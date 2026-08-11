import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  Pressable,
  ActivityIndicator,
  Platform,
  Alert,
  FlatList,
  ScrollView,
} from 'react-native';
import { useRouter } from 'expo-router';
import type {
  DirectMessage,
  FriendRequest,
  PostComment,
  ShareVisibility,
  SocialPost,
} from '@fish-social/shared';
import { SHARE_VISIBILITY_LABELS } from '@fish-social/shared';
import { PostCard } from '../components/PostCard';
import { ProfileAvatar } from '../components/ProfileAvatar';
import { ShopButton } from '../components/ShopButton';
import { CodexButton } from '../components/CodexButton';
import { ShopModal } from '../components/ShopModal';
import { CodexModal } from '../components/CodexModal';
import { AdminDebugButton } from '../components/AdminDebugButton';
import { AppScreen } from '../components/AppScreen';
import { useProfileModal } from '../lib/useProfileModal';
import { getNickname } from '../lib/config';
import { getPlayerId } from '../lib/playerId';
import { socialApi } from '../lib/socialApi';
import { useProfile } from '../lib/useProfile';
import { useShop } from '../lib/useShop';
import { useRequireAuth } from '../lib/useRequireAuth';
import { io } from 'socket.io-client';
import { SOCKET_URL } from '../lib/config';
import { colors, spacing, radius } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

type Tab = 'wall' | 'friends-feed' | 'leaderboard' | 'friends' | 'dm' | 'settings';

function toast(title: string, msg: string) {
  if (Platform.OS === 'web') window.alert(`${title}\n${msg}`);
  else Alert.alert(title, msg);
}

export default function SocialScreen() {
  const router = useRouter();
  const { ready, authenticated } = useRequireAuth();
  const playerId = useMemo(() => getPlayerId(), []);
  const nickname = useMemo(() => getNickname(), []);
  const { profile, setProfile, refresh: refreshProfile } = useProfile(playerId, nickname);
  const shop = useShop(playerId);
  const [shopOpen, setShopOpen] = useState(false);
  const [codexOpen, setCodexOpen] = useState(false);

  const openShop = useCallback(async () => {
    await shop.refresh();
    setShopOpen(true);
  }, [shop]);

  const [tab, setTab] = useState<Tab>('wall');
  const [posts, setPosts] = useState<SocialPost[]>([]);
  const [loading, setLoading] = useState(false);
  const [incoming, setIncoming] = useState<FriendRequest[]>([]);
  const [outgoing, setOutgoing] = useState<FriendRequest[]>([]);
  const [friends, setFriends] = useState<{ playerId: string; nickname: string; avatarUrl?: string }[]>([]);
  const [friendIds, setFriendIds] = useState<string[]>([]);
  const [pendingOutgoingIds, setPendingOutgoingIds] = useState<string[]>([]);
  const [searchQ, setSearchQ] = useState('');
  const [searchResults, setSearchResults] = useState<
    { playerId: string; nickname: string; avatarUrl?: string }[]
  >([]);
  const [dmFriend, setDmFriend] = useState<{ id: string; name: string } | null>(null);
  const [dmMessages, setDmMessages] = useState<DirectMessage[]>([]);
  const [dmText, setDmText] = useState('');
  const [conversations, setConversations] = useState<
    { friendPlayerId: string; friendNickname: string; lastMessage: string }[]
  >([]);
  const { isMobile, isCompact, contentPadding } = useResponsive();

  const loadFriends = useCallback(async () => {
    try {
      const [{ friends: f }, { incoming: inc, outgoing: out }] = await Promise.all([
        socialApi.getFriends(playerId),
        socialApi.getRequests(playerId),
      ]);
      setFriends(f);
      setFriendIds(f.map((x) => x.playerId));
      setIncoming(inc);
      setOutgoing(out);
      setPendingOutgoingIds(out.map((r) => r.toPlayerId));
    } catch {
      /* demo offline */
    }
  }, [playerId]);

  const { openProfile, profileModal } = useProfileModal({
    viewerPlayerId: playerId,
    viewerNickname: nickname,
    friendIds,
    pendingOutgoingIds,
    setFriendIds,
    setPendingOutgoingIds,
    refreshFriends: loadFriends,
    onEditProfile: () => router.push('/profile'),
  });

  const loadPosts = useCallback(async () => {
    setLoading(true);
    try {
      if (tab === 'wall') {
        const { posts: p } = await socialApi.getWall();
        setPosts(p);
      } else if (tab === 'friends-feed') {
        const { posts: p } = await socialApi.getFriendsFeed(playerId);
        setPosts(p);
      }
    } catch {
      setPosts([]);
    } finally {
      setLoading(false);
    }
  }, [tab, playerId]);

  const loadConversations = useCallback(async () => {
    try {
      const { conversations: c } = await socialApi.getConversations(playerId);
      setConversations(c);
    } catch {
      setConversations([]);
    }
  }, [playerId]);

  useEffect(() => {
    loadFriends();
  }, [loadFriends]);

  useEffect(() => {
    if (tab === 'wall' || tab === 'friends-feed') loadPosts();
    if (tab === 'friends') loadFriends();
    if (tab === 'dm') loadConversations();
  }, [tab, loadPosts, loadFriends, loadConversations]);

  const patchPost = useCallback((postId: string, patch: Partial<SocialPost>) => {
    setPosts((prev) => prev.map((p) => (p.id === postId ? { ...p, ...patch } : p)));
  }, []);

  useEffect(() => {
    const socket = io(SOCKET_URL, { transports: ['websocket'] });
    socket.emit('register_player', playerId);
    socket.on('friend_request', () => loadFriends());
    socket.on('dm_message', (msg: DirectMessage) => {
      if (dmFriend && (msg.fromPlayerId === dmFriend.id || msg.toPlayerId === dmFriend.id)) {
        setDmMessages((prev) => [...prev, msg]);
      }
      loadConversations();
    });
    socket.on(
      'post_liked',
      (payload: { postId: string; playerId: string; liked: boolean; likeCount: number }) => {
        setPosts((prev) =>
          prev.map((p) => {
            if (p.id !== payload.postId) return p;
            return {
              ...p,
              likeCount: payload.likeCount,
              likedByMe:
                payload.playerId === playerId ? payload.liked : (p.likedByMe ?? false),
            };
          }),
        );
      },
    );
    socket.on(
      'post_commented',
      (payload: { postId: string; comment: PostComment }) => {
        setPosts((prev) =>
          prev.map((p) =>
            p.id === payload.postId
              ? { ...p, commentCount: (p.commentCount ?? 0) + 1 }
              : p,
          ),
        );
      },
    );
    socket.on(
      'post_comment_deleted',
      (payload: { postId: string; commentId: string; commentCount: number }) => {
        setPosts((prev) =>
          prev.map((p) =>
            p.id === payload.postId ? { ...p, commentCount: payload.commentCount } : p,
          ),
        );
      },
    );
    return () => {
      socket.disconnect();
    };
  }, [playerId, dmFriend, loadFriends, loadConversations]);

  const openDm = async (friendId: string, friendName: string) => {
    setDmFriend({ id: friendId, name: friendName });
    try {
      const { messages } = await socialApi.getMessages(playerId, friendId);
      setDmMessages(messages);
    } catch {
      setDmMessages([]);
    }
  };

  const sendDm = async () => {
    if (!dmFriend || !dmText.trim()) return;
    try {
      const { message } = await socialApi.sendDm(playerId, nickname, dmFriend.id, dmText);
      setDmMessages((prev) => [...prev, message]);
      setDmText('');
    } catch (e) {
      toast('发送失败', e instanceof Error ? e.message : '未知错误');
    }
  };

  const tabs: { id: Tab; label: string }[] = [
    { id: 'wall', label: '动态墙' },
    { id: 'friends-feed', label: '好友动态' },
    { id: 'leaderboard', label: isCompact ? '排行' : '排行榜' },
    { id: 'friends', label: '好友' },
    { id: 'dm', label: '私聊' },
    { id: 'settings', label: '设置' },
  ];

  const renderPost = useCallback(
    ({ item }: { item: SocialPost }) => (
      <PostCard
        post={item}
        viewerPlayerId={playerId}
        onPostPatch={patchPost}
        onPressUser={(user) => openProfile(user)}
      />
    ),
    [playerId, patchPost, openProfile],
  );

  if (!ready || !authenticated) return null;

  return (
    <AppScreen>
      <View style={styles.main}>
        <View style={[styles.topBar, { paddingHorizontal: contentPadding }]}>
          <View style={styles.header}>
            <Pressable onPress={() => router.back()} hitSlop={8}>
              <Text style={styles.back}>{isCompact ? '←' : '← 返回'}</Text>
            </Pressable>
            <Text style={[styles.title, isMobile && styles.titleMobile]}>社交中心</Text>
            <View style={styles.headerActions}>
              <ShopButton onPress={openShop} />
              <CodexButton onPress={() => setCodexOpen(true)} />
              <AdminDebugButton compact={isMobile} />
              <Text style={styles.coins}>💰 {profile?.coins ?? 0}</Text>
            </View>
          </View>

          <ScrollView
            horizontal
            showsHorizontalScrollIndicator={false}
            style={styles.tabs}
            contentContainerStyle={styles.tabsContent}
          >
            {tabs.map((t) => (
              <Pressable
                key={t.id}
                style={[styles.tab, tab === t.id && styles.tabActive, isMobile && styles.tabMobile]}
                onPress={() => { setTab(t.id); setDmFriend(null); }}
              >
                <Text style={[styles.tabText, tab === t.id && styles.tabTextActive]}>
                  {isCompact && t.id === 'friends-feed'
                    ? '动态'
                    : t.label}
                </Text>
              </Pressable>
            ))}
          </ScrollView>
        </View>

        <View style={styles.content}>
            {(tab === 'wall' || tab === 'friends-feed') && (
              loading ? (
                <ActivityIndicator style={styles.loader} color="#4A90A4" />
              ) : (
                <FlatList
                  style={styles.feedList}
                  data={posts}
                  keyExtractor={(p) => p.id}
                  contentContainerStyle={[styles.list, { paddingHorizontal: contentPadding }]}
                  ListEmptyComponent={
                    <Text style={styles.empty}>
                      {tab === 'friends-feed'
                        ? '添加好友后这里会显示他们的鱼获'
                        : '暂无动态'}
                    </Text>
                  }
                  renderItem={renderPost}
                />
              )
            )}

            {tab === 'leaderboard' && (
              <View style={{ padding: 16, alignItems: 'center' }}>
                <Text style={{ fontSize: 15, color: '#666', marginBottom: 12 }}>
                  排行榜已迁移至独立页面
                </Text>
                <Pressable
                  style={{
                    backgroundColor: '#4A90A4',
                    paddingHorizontal: 20,
                    paddingVertical: 10,
                    borderRadius: 10,
                    cursor: 'pointer',
                  }}
                  onPress={() => router.push('/leaderboard')}
                >
                  <Text style={{ color: '#fff', fontWeight: '600' }}>前往排行榜</Text>
                </Pressable>
              </View>
            )}

            {tab === 'friends' && (
              <ScrollView style={styles.scrollContent} contentContainerStyle={styles.list}>
                <Text style={styles.section}>添加好友（输入玩家 ID）</Text>
                <View style={styles.searchRow}>
                  <TextInput
                    style={styles.input}
                    value={searchQ}
                    onChangeText={setSearchQ}
                    placeholder="搜索昵称或 ID..."
                    placeholderTextColor="#999"
                  />
                  <Pressable
                    style={styles.searchBtn}
                    onPress={async () => {
                      try {
                        const { players } = await socialApi.searchPlayers(searchQ, playerId);
                        setSearchResults(players);
                      } catch { setSearchResults([]); }
                    }}
                  >
                    <Text style={styles.searchBtnText}>搜索</Text>
                  </Pressable>
                </View>
                {searchResults.map((p) => (
                  <View key={p.playerId} style={styles.friendRow}>
                    <Pressable
                      style={styles.friendInfo}
                      onPress={() =>
                        openProfile({
                          playerId: p.playerId,
                          nickname: p.nickname,
                          avatarUrl: p.avatarUrl,
                        })
                      }
                    >
                      <View style={styles.friendRowMain}>
                        <ProfileAvatar nickname={p.nickname} avatarUrl={p.avatarUrl} size={32} />
                        <View>
                          <Text style={styles.friendName}>{p.nickname}</Text>
                          <Text style={styles.friendId}>{p.playerId.slice(0, 16)}...</Text>
                        </View>
                      </View>
                    </Pressable>
                    <Pressable
                      style={styles.addBtn}
                      onPress={async () => {
                        try {
                          await socialApi.sendFriendRequest(playerId, nickname, p.playerId);
                          toast('已发送', '好友申请已发送');
                        } catch (e) {
                          toast('失败', e instanceof Error ? e.message : '');
                        }
                      }}
                    >
                      <Text style={styles.addBtnText}>申请</Text>
                    </Pressable>
                  </View>
                ))}

                {incoming.length > 0 && (
                  <>
                    <Text style={styles.section}>好友申请</Text>
                    {incoming.map((r) => (
                      <View key={r.id} style={styles.friendRow}>
                        <Text style={styles.friendName}>{r.fromNickname}</Text>
                        <View style={styles.rowActions}>
                          <Pressable
                            style={styles.acceptBtn}
                            onPress={async () => {
                              await socialApi.acceptRequest(playerId, r.id);
                              loadFriends();
                            }}
                          >
                            <Text style={styles.actionLabel}>同意</Text>
                          </Pressable>
                          <Pressable
                            style={styles.rejectBtn}
                            onPress={async () => {
                              await socialApi.rejectRequest(playerId, r.id);
                              loadFriends();
                            }}
                          >
                            <Text style={styles.actionLabel}>拒绝</Text>
                          </Pressable>
                        </View>
                      </View>
                    ))}
                  </>
                )}

                <Text style={styles.section}>我的好友 ({friends.length})</Text>
                {friends.map((f) => (
                  <View key={f.playerId} style={styles.friendRow}>
                    <Pressable
                      style={styles.friendInfo}
                      onPress={() =>
                        openProfile({
                          playerId: f.playerId,
                          nickname: f.nickname,
                          avatarUrl: f.avatarUrl,
                        })
                      }
                    >
                      <View style={styles.friendRowMain}>
                        <ProfileAvatar nickname={f.nickname} avatarUrl={f.avatarUrl} size={32} />
                        <Text style={styles.friendName}>{f.nickname}</Text>
                      </View>
                    </Pressable>
                    <Pressable style={styles.dmBtn} onPress={() => { setTab('dm'); openDm(f.playerId, f.nickname); }}>
                      <Text style={styles.actionLabel}>私聊</Text>
                    </Pressable>
                    <Pressable
                      style={styles.removeBtn}
                      onPress={async () => {
                        const msg = `确定删除好友「${f.nickname}」？`;
                        if (Platform.OS === 'web') {
                          if (!window.confirm(msg)) return;
                        } else {
                          const ok = await new Promise<boolean>((resolve) => {
                            Alert.alert('删除好友', msg, [
                              { text: '取消', style: 'cancel', onPress: () => resolve(false) },
                              { text: '删除', style: 'destructive', onPress: () => resolve(true) },
                            ]);
                          });
                          if (!ok) return;
                        }
                        try {
                          await socialApi.removeFriend(playerId, f.playerId);
                          await loadFriends();
                          toast('已删除', '好友已移除');
                        } catch (e) {
                          toast('失败', e instanceof Error ? e.message : '');
                        }
                      }}
                    >
                      <Text style={styles.actionLabel}>删除</Text>
                    </Pressable>
                  </View>
                ))}
              </ScrollView>
            )}

            {tab === 'dm' && !dmFriend && (
              <FlatList
                style={styles.feedList}
                data={conversations}
                keyExtractor={(c) => c.friendPlayerId}
                contentContainerStyle={styles.list}
                ListEmptyComponent={<Text style={styles.empty}>暂无私聊，先加好友吧</Text>}
                renderItem={({ item }) => (
                  <Pressable
                    style={styles.convRow}
                    onPress={() => openDm(item.friendPlayerId, item.friendNickname)}
                  >
                    <Text style={styles.friendName}>{item.friendNickname}</Text>
                    <Text style={styles.convPreview} numberOfLines={1}>{item.lastMessage}</Text>
                  </Pressable>
                )}
              />
            )}

            {tab === 'dm' && dmFriend && (
              <View style={styles.dmPanel}>
                <Pressable onPress={() => setDmFriend(null)}>
                  <Text style={styles.back}>← 会话列表</Text>
                </Pressable>
                <Text style={styles.dmTitle}>与 {dmFriend.name} 私聊</Text>
                <FlatList
                  data={dmMessages}
                  keyExtractor={(m) => m.id}
                  style={styles.dmList}
                  contentContainerStyle={styles.dmListContent}
                  renderItem={({ item }) => (
                    <View style={[
                      styles.dmBubble,
                      item.fromPlayerId === playerId ? styles.dmMine : styles.dmOther,
                    ]}>
                      <Text style={[
                        styles.dmText,
                        item.fromPlayerId === playerId ? styles.dmTextMine : styles.dmTextOther,
                      ]}>{item.text}</Text>
                    </View>
                  )}
                />
                <View style={styles.dmInputRow}>
                  <TextInput
                    style={styles.input}
                    value={dmText}
                    onChangeText={setDmText}
                    placeholder="输入消息..."
                    placeholderTextColor="#999"
                  />
                  <Pressable style={styles.searchBtn} onPress={sendDm}>
                    <Text style={styles.searchBtnText}>发送</Text>
                  </Pressable>
                </View>
              </View>
            )}

            {tab === 'settings' && (
              <ScrollView style={styles.scrollContent} contentContainerStyle={styles.list}>
                <Text style={styles.section}>分享可见范围（默认）</Text>
                <Text style={styles.hint}>分享鱼到动态时使用的默认隐私设置</Text>
                {(['public', 'friends'] as ShareVisibility[]).map((v) => (
                  <Pressable
                    key={v}
                    style={[
                      styles.settingRow,
                      profile?.shareVisibility === v && styles.settingActive,
                    ]}
                    onPress={async () => {
                      try {
                        const { profile: p } = await socialApi.setVisibility(playerId, v);
                        setProfile(p);
                        toast('已保存', SHARE_VISIBILITY_LABELS[v]);
                      } catch (e) {
                        toast('失败', e instanceof Error ? e.message : '');
                      }
                    }}
                  >
                    <Text style={styles.settingLabel}>{SHARE_VISIBILITY_LABELS[v]}</Text>
                  </Pressable>
                ))}
                <Text style={styles.playerId}>我的 ID：{playerId}</Text>
                <Pressable style={styles.adminLink} onPress={() => router.push('/admin')}>
                  <Text style={styles.adminLinkText}>高级 · 管理员工具</Text>
                </Pressable>
              </ScrollView>
            )}
          </View>

        {profileModal}

        <ShopModal
          visible={shopOpen}
          gear={shop.gear}
          coins={shop.coins || profile?.coins || 0}
          baits={shop.baits}
          tackles={shop.tackles}
          loading={shop.loading}
          error={shop.error}
          catalogOffline={shop.catalogOffline}
          onRetry={shop.refresh}
          onClose={() => setShopOpen(false)}
          onBuyBait={async (baitId, qty) => {
            try {
              const res = await shop.buyBait(baitId, qty);
              setProfile((p) => (p ? { ...p, coins: res.coins } : p));
            } catch (e) {
              toast('购买失败', e instanceof Error ? e.message : '');
            }
          }}
          onBuyTackle={async (tackleId) => {
            try {
              const res = await shop.buyTackle(tackleId);
              setProfile((p) => (p ? { ...p, coins: res.coins } : p));
            } catch (e) {
              toast('购买失败', e instanceof Error ? e.message : '');
            }
          }}
          onEquipBait={async (baitId) => {
            try {
              await shop.equipBait(baitId);
            } catch (e) {
              toast('装备失败', e instanceof Error ? e.message : '');
            }
          }}
          onEquipTackle={async (tackleId) => {
            try {
              await shop.equipTackle(tackleId);
            } catch (e) {
              toast('装备失败', e instanceof Error ? e.message : '');
            }
          }}
        />
        <CodexModal visible={codexOpen} onClose={() => setCodexOpen(false)} />
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  main: { flex: 1, minHeight: 0 },
  topBar: {
    flexShrink: 0,
    backgroundColor: colors.bg,
    zIndex: 20,
    elevation: 8,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
    paddingBottom: spacing.sm,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
    gap: spacing.sm,
    minHeight: 44,
  },
  back: { color: colors.primary, fontWeight: '600', fontSize: 15 },
  title: { flex: 1, fontSize: 20, fontWeight: '800', color: colors.primaryDark },
  titleMobile: { fontSize: 18 },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    flexShrink: 0,
  },
  coins: { fontSize: 13, color: colors.gold, fontWeight: '700' },
  tabs: { flexGrow: 0 },
  tabsContent: { alignItems: 'center', gap: spacing.sm },
  tab: {
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: radius.pill,
    backgroundColor: '#e8e8e8',
    cursor: 'pointer',
    minHeight: 36,
    justifyContent: 'center',
  },
  tabMobile: {
    paddingHorizontal: 12,
    paddingVertical: 7,
  },
  tabActive: { backgroundColor: colors.primary },
  tabText: { fontSize: 13, color: colors.textSecondary },
  tabTextActive: { color: '#fff', fontWeight: '600' },
  content: { flex: 1, minHeight: 0 },
  feedList: { flex: 1 },
  scrollContent: { flex: 1 },
  loader: { marginTop: 40 },
  list: { padding: 16, paddingBottom: 40 },
  empty: { textAlign: 'center', color: '#aaa', marginTop: 40 },
  section: { fontSize: 15, fontWeight: '700', color: '#333', marginTop: 16, marginBottom: 8 },
  hint: { fontSize: 12, color: '#888', marginBottom: 8 },
  searchRow: { flexDirection: 'row', gap: 8, marginBottom: 12 },
  input: {
    flex: 1,
    backgroundColor: '#fff',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 14,
    borderWidth: 1,
    borderColor: '#ddd',
  },
  searchBtn: {
    backgroundColor: '#4A90A4',
    borderRadius: 10,
    paddingHorizontal: 16,
    justifyContent: 'center',
    cursor: 'pointer',
  },
  searchBtnText: { color: '#fff', fontWeight: '600' },
  friendRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    padding: 12,
    borderRadius: 10,
    marginBottom: 8,
    gap: 8,
  },
  friendInfo: { flex: 1, cursor: 'pointer' },
  friendRowMain: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  friendName: { fontWeight: '600', color: '#333' },
  friendId: { fontSize: 11, color: '#aaa', marginTop: 2 },
  addBtn: { backgroundColor: '#7B68A6', borderRadius: 8, padding: 8, cursor: 'pointer' },
  addBtnText: { color: '#fff', fontSize: 12 },
  rowActions: { flexDirection: 'row', gap: 6 },
  acceptBtn: { backgroundColor: '#4CAF50', borderRadius: 8, padding: 8 },
  rejectBtn: { backgroundColor: '#999', borderRadius: 8, padding: 8 },
  dmBtn: { backgroundColor: '#4A90A4', borderRadius: 8, padding: 8, cursor: 'pointer' },
  removeBtn: { backgroundColor: '#c62828', borderRadius: 8, padding: 8, cursor: 'pointer' },
  actionLabel: { color: '#fff', fontSize: 12 },
  convRow: {
    backgroundColor: '#fff',
    padding: 14,
    borderRadius: 10,
    marginBottom: 8,
    cursor: 'pointer',
  },
  convPreview: { color: '#888', fontSize: 13, marginTop: 4 },
  dmPanel: { flex: 1, padding: 16, minHeight: 0 },
  dmTitle: { fontSize: 16, fontWeight: '700', marginVertical: 8, color: '#333' },
  dmList: { flex: 1, minHeight: 0 },
  dmListContent: { paddingBottom: 12 },
  dmBubble: {
    maxWidth: '80%',
    padding: 10,
    borderRadius: 12,
    marginBottom: 8,
  },
  dmMine: { alignSelf: 'flex-end', backgroundColor: '#4A90A4' },
  dmOther: { alignSelf: 'flex-start', backgroundColor: '#eee' },
  dmText: { fontSize: 14 },
  dmTextMine: { color: '#fff' },
  dmTextOther: { color: '#333' },
  dmInputRow: { flexDirection: 'row', gap: 8, marginTop: 8 },
  settingRow: {
    backgroundColor: '#fff',
    padding: 14,
    borderRadius: 10,
    marginBottom: 8,
    borderWidth: 2,
    borderColor: 'transparent',
    cursor: 'pointer',
  },
  settingActive: { borderColor: '#4A90A4' },
  settingLabel: { fontSize: 15, color: '#333' },
  playerId: { fontSize: 11, color: '#999', marginTop: 24 },
  adminLink: { marginTop: 16, padding: 8, cursor: 'pointer' },
  adminLinkText: { color: '#999', fontSize: 12 },
});

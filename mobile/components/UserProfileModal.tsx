import {
  View,
  Text,
  StyleSheet,
  Modal,
  Pressable,
  ActivityIndicator,
  Platform,
  ScrollView,
} from 'react-native';
import { useEffect, useState } from 'react';
import type { FishInventoryItem, PublicPlayerView } from '@fish-social/shared';
import {
  SHOWCASE_SLOT_COUNT,
  formatFishSize,
  getQualityInfo,
  getSpecies,
} from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';
import { PostCard } from './PostCard';
import { socialApi } from '../lib/socialApi';
import { colors, spacing, radius } from '../lib/theme';

export interface FriendAddedResult {
  playerId: string;
  becameFriend: boolean;
}

interface Props {
  visible: boolean;
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  viewerPlayerId: string;
  viewerNickname: string;
  friendIds: string[];
  pendingOutgoingIds?: string[];
  onClose: () => void;
  onModalHide?: () => void;
  onFriendAdded?: (result: FriendAddedResult) => void;
  onEditProfile?: () => void;
}

export function UserProfileModal({
  visible,
  playerId,
  nickname,
  avatarUrl: initialAvatarUrl,
  viewerPlayerId,
  viewerNickname,
  friendIds,
  pendingOutgoingIds = [],
  onClose,
  onModalHide,
  onFriendAdded,
  onEditProfile,
}: Props) {
  const [loading, setLoading] = useState(false);
  const [viewLoading, setViewLoading] = useState(false);
  const [viewError, setViewError] = useState<string | null>(null);
  const [publicView, setPublicView] = useState<PublicPlayerView | null>(null);
  const [statusText, setStatusText] = useState('');
  const [displayName, setDisplayName] = useState(nickname);
  const [displayAvatar, setDisplayAvatar] = useState(initialAvatarUrl);
  const [requestSent, setRequestSent] = useState(false);
  const [becameFriendThisSession, setBecameFriendThisSession] = useState(false);

  const isSelf = playerId === viewerPlayerId;
  const isBot = playerId.startsWith('bot-');
  const isFriend = friendIds.includes(playerId) || becameFriendThisSession;
  const hasPendingOutgoing = pendingOutgoingIds.includes(playerId) || requestSent;

  useEffect(() => {
    if (!visible) return;

    setRequestSent(false);
    setBecameFriendThisSession(false);
    setStatusText('');
    setLoading(false);
    setDisplayName(nickname);
    setDisplayAvatar(initialAvatarUrl);
    setViewLoading(true);
    setViewError(null);

    socialApi
      .getPublicView(playerId, viewerPlayerId)
      .then(({ view }) => {
        setPublicView(view);
        setDisplayName(view.profile.nickname);
        setDisplayAvatar(view.profile.avatarUrl);
      })
      .catch((e) => {
        setPublicView(null);
        setViewError(e instanceof Error ? e.message : '加载失败');
      })
      .finally(() => setViewLoading(false));
  }, [visible, playerId, nickname, initialAvatarUrl, viewerPlayerId]);

  const handleAddFriend = async () => {
    setLoading(true);
    setStatusText('');
    try {
      const res = await socialApi.sendFriendRequest(viewerPlayerId, viewerNickname, playerId);
      const becameFriend = !!(res.autoAccepted || isBot);
      setRequestSent(true);
      if (becameFriend) {
        setBecameFriendThisSession(true);
        setStatusText('你们已成为好友');
      } else {
        setStatusText('好友申请已发送');
      }
      onFriendAdded?.({ playerId, becameFriend });
    } catch (e) {
      setStatusText(e instanceof Error ? e.message : '发送失败');
    } finally {
      setLoading(false);
    }
  };

  const handleEditProfile = () => {
    onClose();
    onEditProfile?.();
  };

  const showcaseFish = publicView?.showcaseFish ?? [];
  const posts = publicView?.posts ?? [];
  const bio = publicView?.profile.bio?.trim() ?? '';

  const profileSections =
    viewError ? (
      <Text style={styles.errorText}>{viewError}</Text>
    ) : viewLoading ? null : (
      <>
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>简介</Text>
          <Text style={styles.bio}>{bio || '暂无简介'}</Text>
        </View>
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>收藏品</Text>
          <ShowcaseGrid fish={showcaseFish} />
        </View>
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>动态</Text>
          {posts.length === 0 ? (
            <Text style={styles.emptyPosts}>暂无动态</Text>
          ) : (
            posts.map((post) => (
              <PostCard
                key={post.id}
                post={post}
                viewerPlayerId={viewerPlayerId}
                onPostPatch={(postId, patch) => {
                  setPublicView((prev) => {
                    if (!prev) return prev;
                    return {
                      ...prev,
                      posts: prev.posts.map((p) =>
                        p.id === postId ? { ...p, ...patch } : p,
                      ),
                    };
                  });
                }}
              />
            ))
          )}
        </View>
      </>
    );

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
      onDismiss={onModalHide}
    >
      <Pressable style={styles.backdrop} onPress={onClose}>
        <Pressable
          style={[styles.card, Platform.OS === 'web' && styles.cardWeb]}
          onPress={(e) => e.stopPropagation()}
        >
          <ScrollView
            style={styles.scroll}
            contentContainerStyle={styles.scrollContent}
            showsVerticalScrollIndicator
          >
            {viewLoading ? (
              <ActivityIndicator color={colors.primary} style={styles.headerLoader} />
            ) : (
              <ProfileAvatar nickname={displayName} avatarUrl={displayAvatar} size={72} />
            )}
            <Text style={styles.name}>{displayName}</Text>
            {isBot ? <Text style={styles.botBadge}>🤖 钓鱼机器人</Text> : null}
            <Text style={styles.id} numberOfLines={1}>
              ID: {playerId}
            </Text>

            {isSelf ? (
              <>
                {onEditProfile ? (
                  <Pressable style={styles.editBtn} onPress={handleEditProfile}>
                    <Text style={styles.editBtnText}>编辑资料</Text>
                  </Pressable>
                ) : null}
                {profileSections}
              </>
            ) : (
              <>
                {isFriend ? (
                  <Text style={styles.hint}>已是好友</Text>
                ) : hasPendingOutgoing ? (
                  <Text style={styles.hint}>好友申请已发送，等待对方同意</Text>
                ) : (
                  <Pressable
                    style={[styles.addBtn, loading && styles.addBtnDisabled]}
                    onPress={handleAddFriend}
                    disabled={loading}
                  >
                    {loading ? (
                      <ActivityIndicator color="#fff" size="small" />
                    ) : (
                      <Text style={styles.addBtnText}>添加好友</Text>
                    )}
                  </Pressable>
                )}

                {statusText ? <Text style={styles.status}>{statusText}</Text> : null}
                {profileSections}
              </>
            )}
          </ScrollView>

          <Pressable style={styles.closeBtn} onPress={onClose}>
            <Text style={styles.closeText}>关闭</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

function ShowcaseGrid({ fish }: { fish: (FishInventoryItem | null)[] }) {
  const slots = Array.from({ length: SHOWCASE_SLOT_COUNT }, (_, i) => fish[i] ?? null);

  return (
    <View style={styles.showcaseGrid}>
      {slots.map((item, i) => (
        <View key={i} style={styles.showcaseSlot}>
          {item ? (
            <ShowcaseSlot fish={item} />
          ) : (
            <Text style={styles.slotEmpty}>—</Text>
          )}
        </View>
      ))}
    </View>
  );
}

function ShowcaseSlot({ fish }: { fish: FishInventoryItem }) {
  const species = getSpecies(fish.speciesId);
  const quality = getQualityInfo(fish.quality);

  return (
    <>
      <Text style={styles.slotIcon}>{species.icon}</Text>
      <Text style={[styles.slotName, { color: quality.color }]} numberOfLines={1}>
        {species.name}
      </Text>
      <Text style={styles.slotSize}>{formatFishSize(fish.sizeM)}</Text>
    </>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 16,
  },
  card: {
    width: '100%',
    maxWidth: 400,
    maxHeight: '90%',
    backgroundColor: '#fff',
    borderRadius: 20,
    paddingHorizontal: 20,
    paddingTop: 20,
    paddingBottom: 8,
    ...(Platform.OS === 'web' ? { cursor: 'default' as const, maxHeight: '85vh' as const } : {}),
  },
  cardWeb: {},
  scroll: {
    flexGrow: 0,
  },
  scrollContent: {
    alignItems: 'center',
    paddingBottom: spacing.sm,
  },
  headerLoader: {
    height: 72,
  },
  name: {
    marginTop: 12,
    fontSize: 20,
    fontWeight: '800',
    color: '#2C5F6F',
  },
  botBadge: {
    marginTop: 4,
    fontSize: 12,
    color: '#7B68A6',
    fontWeight: '600',
  },
  id: {
    marginTop: 6,
    fontSize: 11,
    color: '#999',
    maxWidth: '100%',
  },
  hint: {
    marginTop: 18,
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    paddingHorizontal: 8,
  },
  addBtn: {
    marginTop: 18,
    backgroundColor: '#7B68A6',
    borderRadius: 12,
    paddingHorizontal: 24,
    paddingVertical: 12,
    minWidth: 140,
    alignItems: 'center',
    cursor: 'pointer',
  },
  addBtnDisabled: {
    opacity: 0.7,
  },
  addBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 15,
  },
  editBtn: {
    marginTop: spacing.md,
    backgroundColor: colors.primary,
    borderRadius: 12,
    paddingHorizontal: 24,
    paddingVertical: 12,
    minWidth: 160,
    alignItems: 'center',
    cursor: 'pointer',
  },
  editBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 15,
  },
  status: {
    marginTop: 10,
    fontSize: 13,
    color: '#4A90A4',
    textAlign: 'center',
  },
  errorText: {
    marginTop: 16,
    fontSize: 13,
    color: colors.danger,
    textAlign: 'center',
  },
  section: {
    alignSelf: 'stretch',
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.borderLight,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#2C5F6F',
    marginBottom: spacing.sm,
  },
  bio: {
    fontSize: 14,
    color: '#666',
    lineHeight: 22,
  },
  showcaseGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    justifyContent: 'center',
  },
  showcaseSlot: {
    width: '22%',
    minWidth: 68,
    aspectRatio: 1,
    borderWidth: 2,
    borderColor: '#ddd',
    borderRadius: radius.sm,
    borderStyle: 'dashed',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#F8FAFB',
    padding: 4,
  },
  slotEmpty: {
    fontSize: 18,
    color: '#ccc',
  },
  slotIcon: {
    fontSize: 20,
  },
  slotName: {
    fontSize: 9,
    fontWeight: '700',
    marginTop: 2,
  },
  slotSize: {
    fontSize: 8,
    color: '#888',
  },
  emptyPosts: {
    fontSize: 13,
    color: '#aaa',
    textAlign: 'center',
    paddingVertical: 12,
  },
  closeBtn: {
    marginTop: 8,
    padding: 8,
    alignItems: 'center',
    cursor: 'pointer',
  },
  closeText: {
    color: '#888',
    fontSize: 14,
  },
});

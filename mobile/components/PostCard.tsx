import { useCallback, useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  Image,
  TextInput,
  ActivityIndicator,
  Platform,
  Alert,
  Animated,
  ScrollView,
} from 'react-native';
import {
  POST_COMMENT_MAX_LENGTH,
  calcFishWeightKg,
  formatFishSize,
  formatFishWeight,
  getQualityInfo,
  getSpecies,
  SHARE_VISIBILITY_LABELS,
  type PostComment,
  type SocialPost,
} from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';
import { resolveAssetUrl } from '../lib/avatarUrl';
import { useResponsive } from '../lib/responsive';
import { socialApi } from '../lib/socialApi';
import { colors, radius, spacing } from '../lib/theme';

interface Props {
  post: SocialPost;
  viewerPlayerId: string;
  /** 动态作者可删本帖任意评论；评论作者可删自己的 */
  onPostPatch?: (postId: string, patch: Partial<SocialPost>) => void;
  onPressUser?: (user: { playerId: string; nickname: string; avatarUrl?: string }) => void;
}

const PHOTO_ASPECT = 4 / 3;

function toast(title: string, msg: string) {
  if (Platform.OS === 'web') window.alert(`${title}\n${msg}`);
  else Alert.alert(title, msg);
}

function formatCommentTime(ts: number): string {
  const d = new Date(ts);
  const now = Date.now();
  const diff = now - ts;
  if (diff < 60_000) return '刚刚';
  if (diff < 3_600_000) return `${Math.floor(diff / 60_000)} 分钟前`;
  if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)} 小时前`;
  return d.toLocaleString();
}

export function PostCard({ post, viewerPlayerId, onPostPatch, onPressUser }: Props) {
  const { isMobile } = useResponsive();
  const species = getSpecies(post.fish.speciesId);
  const quality = getQualityInfo(post.fish.quality);
  const photoUri = resolveAssetUrl(post.photoUrl);
  const [photoError, setPhotoError] = useState(false);

  const likeCount = post.likeCount ?? 0;
  const commentCount = post.commentCount ?? 0;
  const likedByMe = post.likedByMe ?? false;

  const [liking, setLiking] = useState(false);
  const [commentsOpen, setCommentsOpen] = useState(false);
  const [comments, setComments] = useState<PostComment[]>([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [commentsError, setCommentsError] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [sending, setSending] = useState(false);
  const heartScale = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    setPhotoError(false);
  }, [photoUri]);

  const loadComments = useCallback(async () => {
    setCommentsLoading(true);
    setCommentsError(null);
    try {
      const res = await socialApi.getComments(post.id);
      setComments(res.comments);
      onPostPatch?.(post.id, { commentCount: res.commentCount });
    } catch (e) {
      setComments([]);
      setCommentsError(e instanceof Error ? e.message : '评论加载失败');
    } finally {
      setCommentsLoading(false);
    }
  }, [post.id, onPostPatch]);

  useEffect(() => {
    if (commentsOpen) loadComments();
  }, [commentsOpen, loadComments]);

  const pulseHeart = () => {
    heartScale.setValue(1);
    Animated.sequence([
      Animated.timing(heartScale, { toValue: 1.25, duration: 90, useNativeDriver: true }),
      Animated.timing(heartScale, { toValue: 1, duration: 120, useNativeDriver: true }),
    ]).start();
  };

  const onToggleLike = async () => {
    if (liking) return;
    const prevLiked = likedByMe;
    const prevCount = likeCount;
    const nextLiked = !prevLiked;
    const nextCount = Math.max(0, prevCount + (nextLiked ? 1 : -1));
    onPostPatch?.(post.id, { likedByMe: nextLiked, likeCount: nextCount });
    pulseHeart();
    setLiking(true);
    try {
      const res = await socialApi.toggleLike(post.id);
      onPostPatch?.(post.id, { likedByMe: res.liked, likeCount: res.likeCount });
    } catch (e) {
      onPostPatch?.(post.id, { likedByMe: prevLiked, likeCount: prevCount });
      toast('点赞失败', e instanceof Error ? e.message : '请稍后重试');
    } finally {
      setLiking(false);
    }
  };

  const onSendComment = async () => {
    const text = draft.trim();
    if (!text) return;
    if (text.length > POST_COMMENT_MAX_LENGTH) {
      toast('太长了', `评论最多 ${POST_COMMENT_MAX_LENGTH} 字`);
      return;
    }
    setSending(true);
    try {
      const res = await socialApi.postComment(post.id, text);
      setComments((prev) => [...prev, res.comment]);
      onPostPatch?.(post.id, { commentCount: res.commentCount });
      setDraft('');
    } catch (e) {
      toast('发送失败', e instanceof Error ? e.message : '请稍后重试');
    } finally {
      setSending(false);
    }
  };

  const canDeleteComment = (c: PostComment) =>
    c.playerId === viewerPlayerId || post.playerId === viewerPlayerId;

  const onDeleteComment = async (c: PostComment) => {
    const msg = '确定删除这条评论？';
    if (Platform.OS === 'web') {
      if (!window.confirm(msg)) return;
    } else {
      const ok = await new Promise<boolean>((resolve) => {
        Alert.alert('删除评论', msg, [
          { text: '取消', style: 'cancel', onPress: () => resolve(false) },
          { text: '删除', style: 'destructive', onPress: () => resolve(true) },
        ]);
      });
      if (!ok) return;
    }
    try {
      const res = await socialApi.deleteComment(post.id, c.id);
      setComments((prev) => prev.filter((x) => x.id !== c.id));
      onPostPatch?.(post.id, { commentCount: res.commentCount });
    } catch (e) {
      toast('删除失败', e instanceof Error ? e.message : '');
    }
  };

  return (
    <View style={styles.card}>
      <Pressable
        style={styles.userRow}
        onPress={() =>
          onPressUser?.({
            playerId: post.playerId,
            nickname: post.nickname,
            avatarUrl: post.authorAvatarUrl,
          })
        }
        disabled={!onPressUser}
      >
        <ProfileAvatar
          nickname={post.nickname}
          avatarUrl={post.authorAvatarUrl}
          size={isMobile ? 32 : 36}
        />
        <View style={styles.userMeta}>
          <Text style={styles.nickname}>{post.nickname}</Text>
          <Text style={styles.time}>{new Date(post.createdAt).toLocaleString()}</Text>
        </View>
      </Pressable>

      {photoUri && !photoError ? (
        <View style={[styles.photoFrame, isMobile ? styles.photoFrameMobile : styles.photoFrameDesktop]}>
          <Image
            source={{ uri: photoUri }}
            style={styles.photo}
            resizeMode="contain"
            onError={() => setPhotoError(true)}
          />
        </View>
      ) : null}

      <View style={styles.body}>
        <Text style={[styles.icon, isMobile && styles.iconMobile]}>{species.icon}</Text>
        <View style={styles.info}>
          <Text style={styles.text}>{post.text}</Text>
          <Text style={[styles.quality, { color: quality.color }]}>
            {quality.name} · {formatFishSize(post.fish.sizeM)} ·{' '}
            {formatFishWeight(calcFishWeightKg(post.fish.sizeM))}
          </Text>
          <Text style={styles.vis}>{SHARE_VISIBILITY_LABELS[post.visibility]}</Text>
        </View>
      </View>

      <View style={styles.actions}>
        <Pressable
          style={styles.actionBtn}
          onPress={onToggleLike}
          disabled={liking}
          accessibilityRole="button"
          accessibilityLabel={likedByMe ? '取消点赞' : '点赞'}
        >
          <Animated.Text
            style={[
              styles.actionIcon,
              likedByMe && styles.liked,
              { transform: [{ scale: heartScale }] },
            ]}
          >
            {likedByMe ? '❤️' : '🤍'}
          </Animated.Text>
          <Text style={[styles.actionCount, likedByMe && styles.liked]}>{likeCount}</Text>
        </Pressable>

        <Pressable
          style={styles.actionBtn}
          onPress={() => setCommentsOpen((v) => !v)}
          accessibilityRole="button"
          accessibilityLabel="评论"
        >
          <Text style={styles.actionIcon}>💬</Text>
          <Text style={styles.actionCount}>{commentCount}</Text>
        </Pressable>
      </View>

      {commentsOpen ? (
        <View style={styles.commentsPanel}>
          {commentsLoading ? (
            <ActivityIndicator color={colors.primary} style={{ marginVertical: 8 }} />
          ) : commentsError ? (
            <View style={styles.commentsErrorRow}>
              <Text style={styles.commentsError}>{commentsError}</Text>
              <Pressable onPress={loadComments} hitSlop={8}>
                <Text style={styles.retryLink}>重试</Text>
              </Pressable>
            </View>
          ) : comments.length === 0 ? (
            <Text style={styles.sofa}>还没有评论，来抢沙发吧</Text>
          ) : (
            <ScrollView
              style={styles.commentsScroll}
              contentContainerStyle={styles.commentsScrollContent}
              nestedScrollEnabled
              keyboardShouldPersistTaps="handled"
              showsVerticalScrollIndicator
            >
              {comments.map((c) => (
                <View key={c.id} style={styles.commentRow}>
                  <Pressable
                    onPress={() =>
                      onPressUser?.({
                        playerId: c.playerId,
                        nickname: c.nickname,
                        avatarUrl: c.avatarUrl,
                      })
                    }
                    disabled={!onPressUser}
                  >
                    <ProfileAvatar nickname={c.nickname} avatarUrl={c.avatarUrl} size={28} />
                  </Pressable>
                  <View style={styles.commentBody}>
                    <View style={styles.commentMeta}>
                      <Text style={styles.commentNick}>{c.nickname}</Text>
                      <Text style={styles.commentTime}>{formatCommentTime(c.createdAt)}</Text>
                    </View>
                    <Text style={styles.commentText}>{c.text}</Text>
                  </View>
                  {canDeleteComment(c) ? (
                    <Pressable onPress={() => onDeleteComment(c)} hitSlop={8}>
                      <Text style={styles.deleteBtn}>删除</Text>
                    </Pressable>
                  ) : null}
                </View>
              ))}
            </ScrollView>
          )}

          <View style={styles.composer}>
            <TextInput
              style={styles.commentInput}
              value={draft}
              onChangeText={setDraft}
              placeholder="写评论…"
              placeholderTextColor={colors.textMuted}
              maxLength={POST_COMMENT_MAX_LENGTH + 20}
              multiline
            />
            <Pressable
              style={[
                styles.sendBtn,
                (!draft.trim() || draft.trim().length > POST_COMMENT_MAX_LENGTH || sending) &&
                  styles.sendBtnDisabled,
              ]}
              onPress={onSendComment}
              disabled={!draft.trim() || draft.trim().length > POST_COMMENT_MAX_LENGTH || sending}
            >
              <Text style={styles.sendBtnText}>{sending ? '…' : '发送'}</Text>
            </Pressable>
          </View>
          {draft.trim().length > POST_COMMENT_MAX_LENGTH ? (
            <Text style={styles.overLimit}>最多 {POST_COMMENT_MAX_LENGTH} 字</Text>
          ) : null}
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    borderWidth: 1,
    borderColor: colors.borderLight,
    overflow: 'visible',
  },
  userRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginBottom: spacing.sm,
    cursor: 'pointer',
    minHeight: 44,
  },
  userMeta: {
    flex: 1,
    minWidth: 0,
  },
  nickname: {
    fontWeight: '700',
    color: colors.primaryDark,
    fontSize: 14,
  },
  time: {
    fontSize: 11,
    color: colors.textMuted,
    marginTop: 2,
  },
  photoFrame: {
    width: '100%',
    aspectRatio: PHOTO_ASPECT,
    borderRadius: radius.md,
    marginBottom: spacing.sm,
    backgroundColor: colors.primaryLight,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
  },
  photoFrameMobile: {
    maxHeight: 240,
  },
  photoFrameDesktop: {
    maxHeight: 320,
  },
  photo: {
    width: '100%',
    height: '100%',
  },
  body: {
    flexDirection: 'row',
    gap: spacing.md,
    alignItems: 'center',
  },
  icon: {
    fontSize: 36,
  },
  iconMobile: {
    fontSize: 28,
  },
  info: {
    flex: 1,
    minWidth: 0,
  },
  text: {
    fontSize: 14,
    color: colors.text,
    marginBottom: 4,
  },
  quality: {
    fontSize: 13,
    fontWeight: '600',
  },
  vis: {
    fontSize: 11,
    color: colors.textMuted,
    marginTop: 2,
  },
  actions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.lg,
    marginTop: spacing.md,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.borderLight,
  },
  actionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    minHeight: 36,
    paddingVertical: 4,
    paddingHorizontal: 4,
    cursor: 'pointer',
  },
  actionIcon: {
    fontSize: 18,
  },
  actionCount: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.textSecondary,
    minWidth: 12,
  },
  liked: {
    color: '#E53935',
  },
  commentsPanel: {
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.borderLight,
    zIndex: 2,
  },
  commentsScroll: {
    maxHeight: 220,
    marginBottom: spacing.sm,
  },
  commentsScrollContent: {
    paddingBottom: 4,
  },
  commentsErrorRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: spacing.sm,
  },
  commentsError: {
    flex: 1,
    fontSize: 13,
    color: '#c62828',
  },
  retryLink: {
    fontSize: 13,
    fontWeight: '700',
    color: colors.primary,
  },
  sofa: {
    fontSize: 13,
    color: colors.textMuted,
    marginBottom: spacing.sm,
  },
  commentRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: spacing.sm,
    marginBottom: spacing.sm,
  },
  commentBody: {
    flex: 1,
    minWidth: 0,
  },
  commentMeta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    marginBottom: 2,
  },
  commentNick: {
    fontSize: 13,
    fontWeight: '700',
    color: colors.primaryDark,
  },
  commentTime: {
    fontSize: 11,
    color: colors.textMuted,
  },
  commentText: {
    fontSize: 13,
    color: colors.text,
    lineHeight: 18,
  },
  deleteBtn: {
    fontSize: 12,
    color: '#c62828',
    paddingTop: 2,
  },
  composer: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    gap: 8,
    marginTop: 4,
  },
  commentInput: {
    flex: 1,
    minHeight: 36,
    maxHeight: 88,
    backgroundColor: colors.bg,
    borderRadius: radius.md,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 14,
    borderWidth: 1,
    borderColor: colors.border,
    color: colors.text,
  },
  sendBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingHorizontal: 14,
    paddingVertical: 9,
    cursor: 'pointer',
  },
  sendBtnDisabled: {
    opacity: 0.45,
  },
  sendBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 13,
  },
  overLimit: {
    fontSize: 11,
    color: '#c62828',
    marginTop: 4,
  },
});

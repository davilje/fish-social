import { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable, Platform } from 'react-native';
import { isFishingActive, type ChatMessage, type PondUser } from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';
import { ChatPanel } from './ChatPanel';
import { colors, spacing, radius } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

interface Props {
  users: PondUser[];
  myUserId: string | null;
  messages: ChatMessage[];
  onSend: (text: string) => Promise<{ ok: boolean; error?: string }>;
  expanded?: boolean;
  onPressUser?: (user: { playerId?: string; nickname: string; avatarUrl?: string }) => void;
}

type MobileTab = 'online' | 'chat';

/** 鱼塘在线钓友（纵向）+ 聊天侧栏；竖屏可 Tab 切换 */
export function PondSocialPanel({
  users,
  myUserId,
  messages,
  onSend,
  expanded = false,
  onPressUser,
}: Props) {
  const { isMobile, pondSideBySide } = useResponsive();
  const portrait = isMobile && !pondSideBySide;
  const [mobileTab, setMobileTab] = useState<MobileTab>('chat');

  const onlineList = (
    <View style={[styles.onlineSection, portrait && styles.onlineSectionPortrait]}>
      <Text style={styles.onlineTitle}>在线钓友 ({users.length})</Text>
      {users.length === 0 ? (
        <Text style={styles.onlineEmpty}>暂无其他钓友</Text>
      ) : (
        <ScrollView
          nestedScrollEnabled
          showsVerticalScrollIndicator={Platform.OS !== 'web'}
          style={[styles.onlineScroll, portrait && styles.onlineScrollPortrait]}
          contentContainerStyle={styles.onlineScrollContent}
        >
          {users.map((user) => {
            const fishing = isFishingActive(user.fishingPhase);
            return (
              <Pressable
                key={user.id}
                style={[styles.row, user.id === myUserId && styles.rowMe]}
                onPress={() =>
                  onPressUser?.({
                    playerId: user.playerId,
                    nickname: user.nickname,
                    avatarUrl: user.avatarUrl,
                  })
                }
                disabled={!onPressUser}
              >
                <ProfileAvatar nickname={user.nickname} avatarUrl={user.avatarUrl} size={28} />
                <Text style={styles.rowName} numberOfLines={1}>
                  {user.nickname}
                </Text>
                {fishing ? <View style={styles.dotFishing} /> : null}
              </Pressable>
            );
          })}
        </ScrollView>
      )}
    </View>
  );

  const chat = (
    <View style={[styles.chatSection, portrait && styles.chatSectionPortrait]}>
      <ChatPanel messages={messages} onSend={onSend} embedded expanded={expanded || !portrait} />
    </View>
  );

  if (portrait) {
    return (
      <View style={[styles.panel, styles.panelPortrait]}>
        <View style={styles.mobileTabs}>
          <Pressable
            style={[styles.mobileTab, mobileTab === 'online' && styles.mobileTabActive]}
            onPress={() => setMobileTab('online')}
          >
            <Text style={[styles.mobileTabText, mobileTab === 'online' && styles.mobileTabTextActive]}>
              在线 ({users.length})
            </Text>
          </Pressable>
          <Pressable
            style={[styles.mobileTab, mobileTab === 'chat' && styles.mobileTabActive]}
            onPress={() => setMobileTab('chat')}
          >
            <Text style={[styles.mobileTabText, mobileTab === 'chat' && styles.mobileTabTextActive]}>
              聊天
            </Text>
          </Pressable>
        </View>
        {mobileTab === 'online' ? onlineList : chat}
      </View>
    );
  }

  return (
    <View style={[styles.panel, expanded && styles.panelExpanded]}>
      {onlineList}
      {chat}
    </View>
  );
}

const styles = StyleSheet.create({
  panel: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: radius.lg,
    borderTopRightRadius: radius.lg,
    overflow: 'hidden',
    minHeight: 220,
  },
  panelPortrait: {
    maxHeight: 260,
    minHeight: 200,
    borderRadius: radius.md,
    marginHorizontal: spacing.sm,
    marginBottom: spacing.sm,
  },
  panelExpanded: {
    flex: 1,
    borderRadius: radius.md,
    margin: spacing.md,
    marginLeft: 0,
    minHeight: 400,
  },
  mobileTabs: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    borderBottomColor: colors.borderLight,
  },
  mobileTab: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    cursor: 'pointer',
  },
  mobileTabActive: {
    borderBottomWidth: 2,
    borderBottomColor: colors.primary,
  },
  mobileTabText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.textSecondary,
  },
  mobileTabTextActive: {
    color: colors.primaryDark,
  },
  onlineSection: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.sm,
    paddingBottom: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.borderLight,
    maxHeight: 180,
  },
  onlineSectionPortrait: {
    flex: 1,
    maxHeight: undefined,
    borderBottomWidth: 0,
  },
  onlineTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.primaryDark,
    marginBottom: spacing.sm,
  },
  onlineEmpty: {
    fontSize: 12,
    color: colors.textMuted,
    paddingVertical: 8,
  },
  onlineScroll: {
    maxHeight: 140,
  },
  onlineScrollPortrait: {
    maxHeight: undefined,
    flex: 1,
  },
  onlineScrollContent: {
    paddingBottom: 4,
    gap: 6,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.surfaceMuted,
    borderRadius: radius.md,
    paddingHorizontal: 10,
    paddingVertical: 8,
    cursor: 'pointer',
  },
  rowMe: {
    borderWidth: 1,
    borderColor: colors.gold,
    backgroundColor: '#FFFBEB',
  },
  rowName: {
    flex: 1,
    fontSize: 13,
    fontWeight: '600',
    color: colors.text,
  },
  dotFishing: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#4CAF50',
  },
  chatSection: {
    flex: 1,
    minHeight: 160,
  },
  chatSectionPortrait: {
    flex: 1,
    minHeight: 140,
  },
});

import { View, Text, StyleSheet, ScrollView, Pressable } from 'react-native';
import type { PondUser } from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';

interface Props {
  users: PondUser[];
  myUserId: string | null;
  onPressUser?: (user: { playerId?: string; nickname: string; avatarUrl?: string }) => void;
}

export function OnlineUsersPanel({ users, myUserId, onPressUser }: Props) {
  if (users.length === 0) return null;

  return (
    <View style={styles.panel}>
      <Text style={styles.title}>在线钓友 ({users.length})</Text>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scroll}>
        {users.map((user) => (
          <Pressable
            key={user.id}
            style={[styles.chip, user.id === myUserId && styles.chipMe]}
            onPress={() =>
              onPressUser?.({
                playerId: user.playerId,
                nickname: user.nickname,
                avatarUrl: user.avatarUrl,
              })
            }
            disabled={!onPressUser}
          >
            <ProfileAvatar nickname={user.nickname} avatarUrl={user.avatarUrl} size={26} />
            <Text style={styles.name} numberOfLines={1}>
              {user.nickname}
            </Text>
          </Pressable>
        ))}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  panel: {
    backgroundColor: '#fff',
    marginHorizontal: 12,
    marginBottom: 8,
    borderRadius: 12,
    padding: 12,
  },
  title: {
    fontSize: 12,
    fontWeight: '700',
    color: '#2C5F6F',
    marginBottom: 8,
  },
  scroll: {
    flexGrow: 0,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#F0F7FA',
    borderRadius: 20,
    paddingHorizontal: 10,
    paddingVertical: 6,
    marginRight: 8,
    maxWidth: 220,
    gap: 6,
    cursor: 'pointer',
  },
  chipMe: {
    borderWidth: 1,
    borderColor: '#FFD700',
    backgroundColor: '#FFFBEB',
  },
  name: {
    fontSize: 12,
    fontWeight: '600',
    color: '#333',
    maxWidth: 120,
  },
});

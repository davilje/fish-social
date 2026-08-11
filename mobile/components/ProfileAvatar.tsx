import { View, Text, Image, StyleSheet, Pressable } from 'react-native';
import { resolveAvatarUrl } from '../lib/avatarUrl';

interface Props {
  nickname: string;
  avatarUrl?: string;
  size?: number;
  onPress?: () => void;
}

export function ProfileAvatar({ nickname, avatarUrl, size = 40, onPress }: Props) {
  const resolvedUrl = resolveAvatarUrl(avatarUrl);
  const inner = resolvedUrl ? (
    <Image source={{ uri: resolvedUrl }} style={[styles.img, { width: size, height: size, borderRadius: size / 2 }]} />
  ) : (
    <View style={[styles.fallback, { width: size, height: size, borderRadius: size / 2 }]}>
      <Text style={[styles.letter, { fontSize: size * 0.4 }]}>{nickname.slice(0, 1) || '钓'}</Text>
    </View>
  );

  if (onPress) {
    return (
      <Pressable onPress={onPress} style={({ pressed }) => pressed && styles.pressed}>
        {inner}
      </Pressable>
    );
  }
  return inner;
}

const styles = StyleSheet.create({
  img: {
    borderWidth: 2,
    borderColor: '#fff',
  },
  fallback: {
    backgroundColor: '#4A90A4',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 2,
    borderColor: '#fff',
  },
  letter: {
    color: '#fff',
    fontWeight: '700',
  },
  pressed: {
    opacity: 0.85,
  },
});

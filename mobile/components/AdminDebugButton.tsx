import { Pressable, Text, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { colors, radius } from '../lib/theme';

interface Props {
  compact?: boolean;
}

/** 世界地图顶栏 Debug / 管理入口 */
export function AdminDebugButton({ compact }: Props) {
  const router = useRouter();

  return (
    <Pressable
      style={[styles.btn, compact && styles.btnCompact]}
      onPress={() => router.push('/admin')}
      hitSlop={6}
      accessibilityRole="button"
      accessibilityLabel="管理员工具"
    >
      <Text style={styles.icon}>⚙</Text>
      {!compact && <Text style={styles.label}>Debug</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 6,
    borderRadius: radius.sm,
    backgroundColor: colors.surfaceMuted,
    cursor: 'pointer',
    minHeight: 36,
    justifyContent: 'center',
  },
  btnCompact: {
    paddingHorizontal: 6,
  },
  icon: {
    fontSize: 16,
  },
  label: {
    fontSize: 11,
    fontWeight: '700',
    color: colors.textSecondary,
  },
});

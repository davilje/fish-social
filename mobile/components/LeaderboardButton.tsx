import { Pressable, Text, StyleSheet } from 'react-native';
import { colors, radius, touch } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

export function LeaderboardButton({ onPress }: { onPress: () => void }) {
  const { isMobile } = useResponsive();

  return (
    <Pressable
      style={[styles.btn, isMobile && styles.btnCompact]}
      onPress={onPress}
      hitSlop={4}
      accessibilityLabel="排行榜"
    >
      <Text style={[styles.text, isMobile && styles.textCompact]}>
        {isMobile ? '🏆' : '🏆 排行'}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    backgroundColor: colors.gold,
    borderRadius: radius.pill,
    paddingHorizontal: 14,
    paddingVertical: 8,
    minHeight: touch.minSize,
    minWidth: touch.minSize,
    justifyContent: 'center',
    alignItems: 'center',
    cursor: 'pointer',
  },
  btnCompact: {
    paddingHorizontal: 10,
    minWidth: 40,
    minHeight: 40,
  },
  text: { color: '#fff', fontWeight: '600', fontSize: 13 },
  textCompact: { fontSize: 18 },
});

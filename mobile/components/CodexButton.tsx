import { Pressable, Text, StyleSheet } from 'react-native';
import { colors, radius, touch } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

interface Props {
  onPress: () => void;
}

export function CodexButton({ onPress }: Props) {
  const { isMobile } = useResponsive();

  return (
    <Pressable
      style={({ hovered }: { hovered?: boolean }) => [
        styles.btn,
        isMobile && styles.btnCompact,
        hovered && styles.btnHover,
      ]}
      onPress={onPress}
      hitSlop={4}
      accessibilityLabel="钓鱼图鉴"
    >
      <Text style={[styles.text, isMobile && styles.textCompact]}>
        {isMobile ? '📖' : '📖 图鉴'}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    backgroundColor: colors.primary,
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
  btnHover: {
    backgroundColor: '#3d7a8c',
  },
  text: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 13,
  },
  textCompact: {
    fontSize: 18,
  },
});

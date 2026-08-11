import { View, Text, Pressable, StyleSheet, type ViewStyle } from 'react-native';
import { colors, spacing } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

interface Props {
  title: string;
  subtitle?: string;
  onBack?: () => void;
  backLabel?: string;
  right?: React.ReactNode;
  style?: ViewStyle;
}

/** 统一顶栏：移动端紧凑、桌面端宽松 */
export function AppHeader({
  title,
  subtitle,
  onBack,
  backLabel = '← 返回',
  right,
  style,
}: Props) {
  const { isMobile, isCompact, contentPadding, titleSize } = useResponsive();

  return (
    <View
      style={[
        styles.wrap,
        {
          paddingHorizontal: contentPadding,
          paddingTop: isMobile ? spacing.sm : spacing.md,
          paddingBottom: spacing.sm,
        },
        style,
      ]}
    >
      <View style={styles.row}>
        {onBack ? (
          <Pressable
            onPress={onBack}
            style={styles.backBtn}
            hitSlop={8}
            accessibilityRole="button"
          >
            <Text style={styles.backText}>{isCompact ? '←' : backLabel}</Text>
          </Pressable>
        ) : (
          <View style={styles.backPlaceholder} />
        )}

        <View style={[styles.center, !onBack && styles.centerFlush]}>
          <Text style={[styles.title, { fontSize: titleSize }]} numberOfLines={1}>
            {title}
          </Text>
          {subtitle ? (
            <Text style={styles.subtitle} numberOfLines={1}>
              {subtitle}
            </Text>
          ) : null}
        </View>

        <View style={styles.right}>{right ?? <View style={styles.backPlaceholder} />}</View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    backgroundColor: colors.bg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    minHeight: 44,
  },
  backBtn: {
    minWidth: 44,
    minHeight: 44,
    justifyContent: 'center',
    paddingRight: spacing.sm,
    cursor: 'pointer',
  },
  backPlaceholder: {
    width: 44,
  },
  backText: {
    color: colors.primary,
    fontWeight: '600',
    fontSize: 15,
  },
  center: {
    flex: 1,
    alignItems: 'center',
  },
  centerFlush: {
    alignItems: 'flex-start',
  },
  title: {
    fontWeight: '800',
    color: colors.primaryDark,
  },
  subtitle: {
    marginTop: 2,
    fontSize: 12,
    color: colors.textMuted,
  },
  right: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    minWidth: 44,
    justifyContent: 'flex-end',
  },
});

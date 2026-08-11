import { View, StyleSheet, type ViewStyle } from 'react-native';
import { useResponsive } from '../lib/responsive';

interface Props {
  children: React.ReactNode;
  style?: ViewStyle;
}

/** Web / 桌面端：居中限宽容器 */
export function ScreenShell({ children, style }: Props) {
  const { isDesktop, contentMaxWidth } = useResponsive();

  return (
    <View style={[styles.outer, style]}>
      <View
        style={[
          styles.inner,
          isDesktop && { maxWidth: contentMaxWidth, width: '100%' as const },
        ]}
      >
        {children}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  outer: {
    flex: 1,
    alignItems: 'center',
  },
  inner: {
    flex: 1,
    width: '100%',
    minHeight: 0,
  },
});

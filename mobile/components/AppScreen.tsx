import {
  View,
  ScrollView,
  StyleSheet,
  SafeAreaView,
  Platform,
  type ViewStyle,
} from 'react-native';
import { ScreenShell } from './ScreenShell';
import { colors } from '../lib/theme';
import { useResponsive } from '../lib/responsive';

interface Props {
  children: React.ReactNode;
  backgroundColor?: string;
  /** 小屏下整页可滚动（登录、个人资料等） */
  scroll?: boolean;
  style?: ViewStyle;
  contentStyle?: ViewStyle;
}

/** 统一页面容器：安全区 + 移动端全高 + 桌面居中限宽 */
export function AppScreen({
  children,
  backgroundColor = colors.bg,
  scroll = false,
  style,
  contentStyle,
}: Props) {
  const { isMobile, contentPadding } = useResponsive();

  const body = scroll ? (
    <ScrollView
      style={styles.scroll}
      contentContainerStyle={[
        styles.scrollContent,
        { paddingHorizontal: contentPadding },
        contentStyle,
      ]}
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
    >
      {children}
    </ScrollView>
  ) : (
    <View style={[styles.body, contentStyle]}>{children}</View>
  );

  return (
    <SafeAreaView
      style={[
        styles.safe,
        { backgroundColor },
        Platform.OS === 'web' && styles.safeWeb,
        isMobile && styles.safeMobile,
        style,
      ]}
    >
      <ScreenShell style={styles.shell}>{body}</ScreenShell>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: {
    flex: 1,
  },
  safeWeb: {
    minHeight: '100vh' as unknown as number,
    height: '100vh' as unknown as number,
    overflow: 'hidden',
  },
  safeMobile: {
    maxWidth: '100%',
  },
  shell: {
    flex: 1,
    minHeight: 0,
  },
  body: {
    flex: 1,
    minHeight: 0,
  },
  scroll: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    paddingBottom: spacingBottom(),
  },
});

function spacingBottom() {
  return Platform.OS === 'ios' ? 24 : 16;
}

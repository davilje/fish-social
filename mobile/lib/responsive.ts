import { useWindowDimensions, Platform } from 'react-native';
import { spacing } from './theme';

export const BREAKPOINTS = {
  compact: 400,
  tablet: 768,
  desktop: 1024,
  wide: 1280,
} as const;

export type LayoutMode = 'compact' | 'mobile' | 'tablet' | 'desktop';

export function useResponsive() {
  const { width, height } = useWindowDimensions();
  const isWeb = Platform.OS === 'web';
  const isNative = Platform.OS !== 'web';

  const isCompact = width < BREAKPOINTS.compact;
  const isMobile = isNative || width < BREAKPOINTS.tablet;
  const isTablet = width >= BREAKPOINTS.tablet && width < BREAKPOINTS.desktop;
  const isDesktop = width >= BREAKPOINTS.desktop;
  const isWide = width >= BREAKPOINTS.wide;

  const layoutMode: LayoutMode = isDesktop
    ? 'desktop'
    : isTablet
      ? 'tablet'
      : isCompact
        ? 'compact'
        : 'mobile';

  const contentPadding = isDesktop ? spacing.xl : isTablet ? spacing.lg : spacing.md;
  const contentMaxWidth = isDesktop ? 1200 : isTablet ? 900 : width;
  const pondSideBySide = isWeb && isDesktop;

  return {
    width,
    height,
    isWeb,
    isNative,
    isCompact,
    isMobile,
    isTablet,
    isDesktop,
    isWide,
    layoutMode,
    contentPadding,
    contentMaxWidth,
    pondSideBySide,
    /** 顶栏图标按钮尺寸 */
    iconBtnSize: isMobile ? 40 : 44,
    /** 主标题字号 */
    titleSize: isCompact ? 18 : isMobile ? 20 : 24,
    /** 卡片内边距 */
    cardPadding: isMobile ? spacing.md : spacing.lg,
  };
}

export function scaleToFit(
  contentW: number,
  contentH: number,
  containerW: number,
  containerH: number,
  maxScale = 1.6,
): number {
  const scaleX = containerW / contentW;
  const scaleY = containerH / contentH;
  return Math.min(scaleX, scaleY, maxScale);
}

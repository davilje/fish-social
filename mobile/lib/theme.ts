/** 全局 UI 主题令牌 */
export const colors = {
  bg: '#F5F0E8',
  bgCool: '#E8F4F8',
  surface: '#FFFFFF',
  surfaceMuted: '#F8FAFB',
  primary: '#4A90A4',
  primaryDark: '#2C5F6F',
  primaryLight: '#E8F4F8',
  accent: '#7B68A6',
  success: '#4CAF50',
  danger: '#c62828',
  warning: '#FFB74D',
  gold: '#E6A700',
  text: '#333333',
  textSecondary: '#666666',
  textMuted: '#888888',
  border: '#E0D8CC',
  borderLight: '#EEEEEE',
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
} as const;

export const radius = {
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  pill: 999,
} as const;

export const touch = {
  minSize: 44,
} as const;

export const shadow = {
  card: {
    shadowColor: '#000',
    shadowOpacity: 0.06,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 4 },
    elevation: 3,
  },
} as const;

import { Modal, View, Text, Pressable, StyleSheet } from 'react-native';
import { colors, radius, spacing } from '../lib/theme';

export interface AppNoticeModalProps {
  visible: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  onConfirm: () => void;
  /** 可选次要按钮 */
  cancelLabel?: string;
  onCancel?: () => void;
}

/** FEAT-UI-1：统一自定义提示弹窗（替代系统 Alert） */
export function AppNoticeModal({
  visible,
  title,
  message,
  confirmLabel = '知道了',
  onConfirm,
  cancelLabel,
  onCancel,
}: AppNoticeModalProps) {
  if (!visible) return null;

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onCancel ?? onConfirm}>
      <View style={styles.overlay}>
        <View style={styles.card}>
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.message}>{message}</Text>
          <View style={styles.actions}>
            {cancelLabel && onCancel ? (
              <Pressable style={[styles.btn, styles.btnGhost]} onPress={onCancel}>
                <Text style={styles.btnGhostText}>{cancelLabel}</Text>
              </Pressable>
            ) : null}
            <Pressable style={[styles.btn, styles.btnPrimary]} onPress={onConfirm}>
              <Text style={styles.btnPrimaryText}>{confirmLabel}</Text>
            </Pressable>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
  },
  card: {
    width: '100%',
    maxWidth: 360,
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.borderLight,
  },
  title: {
    fontSize: 18,
    fontWeight: '800',
    color: colors.primaryDark,
    marginBottom: spacing.sm,
    textAlign: 'center',
  },
  message: {
    fontSize: 14,
    color: colors.text,
    lineHeight: 21,
    textAlign: 'center',
    marginBottom: spacing.lg,
  },
  actions: {
    flexDirection: 'row',
    justifyContent: 'center',
    gap: spacing.sm,
  },
  btn: {
    borderRadius: radius.pill,
    paddingHorizontal: 22,
    paddingVertical: 11,
    minHeight: 44,
    justifyContent: 'center',
    cursor: 'pointer',
  },
  btnPrimary: {
    backgroundColor: colors.primary,
  },
  btnPrimaryText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 14,
  },
  btnGhost: {
    backgroundColor: colors.surfaceMuted,
  },
  btnGhostText: {
    color: colors.textSecondary,
    fontWeight: '600',
    fontSize: 14,
  },
});

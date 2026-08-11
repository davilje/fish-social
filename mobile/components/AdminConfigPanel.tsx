import { useCallback, useEffect, useState } from 'react';
import { View, Text, StyleSheet, Pressable, TextInput, ActivityIndicator } from 'react-native';
import type { GameConfigEntryView } from '@fish-social/shared';
import { adminApiClient } from '../lib/adminApi';
import { describeConfigKey } from '../lib/configLabels';
import { colors, spacing, radius } from '../lib/theme';

interface Props {
  adminName?: string;
}

export function AdminConfigPanel({ adminName = 'admin-a' }: Props) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [entries, setEntries] = useState<GameConfigEntryView[]>([]);
  const [pending, setPending] = useState<
    Array<{ id: string; configKey: string; proposedValue: string; submittedBy: string }>
  >([]);
  const [editKey, setEditKey] = useState('FISH_BITE_CHECK_MS');
  const [editValue, setEditValue] = useState('');
  const [approver, setApprover] = useState('admin-b');
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [cfg, pend] = await Promise.all([
        adminApiClient.getConfig(),
        adminApiClient.getConfigPending(),
      ]);
      setEntries(cfg.entries);
      setPending(pend.requests);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : '加载失败');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) load();
  }, [open, load]);

  const submit = async () => {
    setMessage('');
    try {
      await adminApiClient.submitConfigChange(editKey, editValue, adminName);
      setMessage('已提交，等待另一管理员审批');
      load();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : '提交失败');
    }
  };

  const approve = async (requestId: string) => {
    try {
      await adminApiClient.approveConfigChange(requestId, approver);
      setMessage('已批准并热更');
      load();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : '审批失败');
    }
  };

  return (
    <View style={styles.wrap}>
      <Pressable style={styles.header} onPress={() => setOpen((v) => !v)}>
        <Text style={styles.headerTitle}>参数热更 (C1) {open ? '▾' : '▸'}</Text>
      </Pressable>
      {open && (
        <View style={styles.body}>
          {loading ? <ActivityIndicator color={colors.primary} /> : null}
          {message ? <Text style={styles.msg}>{message}</Text> : null}
          <Text style={styles.sub}>生效值 vs 兜底值（前 8 项）</Text>
          {entries.slice(0, 8).map((e) => {
            const meta = describeConfigKey(e.key);
            return (
              <View key={e.key} style={styles.entryRow}>
                <Text style={styles.entryLabel}>{meta.label}</Text>
                <Text style={styles.entryHint}>{meta.hint}</Text>
                <Text style={styles.row}>
                  {e.effectiveValue}
                  {e.effectiveValue !== e.defaultValue ? `（默认 ${e.defaultValue}）` : ''}
                </Text>
              </View>
            );
          })}
          <View style={styles.formRow}>
            <TextInput
              style={styles.input}
              value={editKey}
              onChangeText={setEditKey}
              placeholder="配置键"
            />
            <TextInput
              style={styles.input}
              value={editValue}
              onChangeText={setEditValue}
              placeholder="新值"
            />
          </View>
          <Pressable style={styles.btn} onPress={submit}>
            <Text style={styles.btnText}>提交变更</Text>
          </Pressable>
          <Text style={styles.sub}>待审批 ({pending.length})</Text>
          <TextInput
            style={styles.input}
            value={approver}
            onChangeText={setApprover}
            placeholder="审批人 ID（不能与提交人相同）"
          />
          {pending.map((r) => {
            const meta = describeConfigKey(r.configKey);
            return (
              <View key={r.id} style={styles.pendingRow}>
                <Text style={styles.entryLabel}>{meta.label}</Text>
                <Text style={styles.row}>
                  → {r.proposedValue}（提交：{r.submittedBy}）
                </Text>
                <Pressable style={styles.btnSmall} onPress={() => approve(r.id)}>
                  <Text style={styles.btnText}>批准</Text>
                </Pressable>
              </View>
            );
          })}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: spacing.md },
  header: { padding: spacing.sm, backgroundColor: '#fff', borderRadius: radius.md },
  headerTitle: { fontWeight: '700', color: colors.primaryDark },
  body: { marginTop: spacing.sm, backgroundColor: '#fff', borderRadius: radius.md, padding: spacing.md },
  sub: { fontWeight: '700', marginTop: spacing.sm, marginBottom: 4, color: '#555' },
  entryRow: { marginBottom: 8, paddingBottom: 6, borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: colors.border },
  entryLabel: { fontSize: 13, fontWeight: '700', color: colors.primaryDark },
  entryHint: { fontSize: 11, color: '#888', marginBottom: 2 },
  row: { fontSize: 12, color: '#333', marginBottom: 2 },
  formRow: { flexDirection: 'row', gap: 8, marginTop: spacing.sm },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.sm,
    padding: 8,
    fontSize: 12,
    marginBottom: 6,
  },
  btn: { backgroundColor: colors.primary, borderRadius: radius.md, padding: 10, alignItems: 'center' },
  btnSmall: { backgroundColor: colors.primary, borderRadius: 6, padding: 6, alignSelf: 'flex-start' },
  btnText: { color: '#fff', fontWeight: '700', fontSize: 12 },
  pendingRow: { marginBottom: 8 },
  msg: { color: colors.primary, fontSize: 12, marginBottom: 6 },
});

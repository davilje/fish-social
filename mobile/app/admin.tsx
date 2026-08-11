import { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  Pressable,
  ScrollView,
  ActivityIndicator,
  Platform,
  Alert,
} from 'react-native';
import { useRouter } from 'expo-router';
import {
  PONDS,
  type PondFishEntity,
} from '@fish-social/shared';
import { AppScreen } from '../components/AppScreen';
import { AppHeader } from '../components/AppHeader';
import { AdminPondFishDebugGrid } from '../components/AdminPondFishDebugGrid';
import { AdminConfigPanel } from '../components/AdminConfigPanel';
import { AdminMetricsPanel } from '../components/AdminMetricsPanel';
import { adminApiClient, getStoredAdminKey, setStoredAdminKey } from '../lib/adminApi';
import { colors, spacing, radius } from '../lib/theme';
import { useRequireAuth } from '../lib/useRequireAuth';

function confirmAction(title: string, message: string): Promise<boolean> {
  if (Platform.OS === 'web') {
    return Promise.resolve(window.confirm(`${title}\n${message}`));
  }
  return new Promise((resolve) => {
    Alert.alert(title, message, [
      { text: '取消', style: 'cancel', onPress: () => resolve(false) },
      { text: '确认', style: 'destructive', onPress: () => resolve(true) },
    ]);
  });
}

export default function AdminScreen() {
  const router = useRouter();
  const { ready, authenticated } = useRequireAuth();
  const [adminKey, setAdminKey] = useState(getStoredAdminKey());
  const [authed, setAuthed] = useState(false);
  const [loading, setLoading] = useState(false);
  const [ponds, setPonds] = useState<
    { pondId: string; summary: import('@fish-social/shared').PondEcologySummary }[]
  >([]);
  const [selectedPond, setSelectedPond] = useState<string | null>(null);
  const [pondFish, setPondFish] = useState<PondFishEntity[]>([]);
  const [logs, setLogs] = useState<
    { id: string; message: string; stack?: string; context?: string; createdAt: number }[]
  >([]);
  const [message, setMessage] = useState('');

  const loadAll = useCallback(async () => {
    setLoading(true);
    setMessage('');
    try {
      const [{ ponds: p }, { logs: l }] = await Promise.all([
        adminApiClient.listPonds(),
        adminApiClient.getLogs(80),
      ]);
      setPonds(p);
      setLogs(l);
      setAuthed(true);
    } catch (e) {
      setAuthed(false);
      setMessage(e instanceof Error ? e.message : '认证失败');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (adminKey) loadAll();
  }, []);

  const handleLogin = () => {
    setStoredAdminKey(adminKey.trim());
    loadAll();
  };

  const loadFish = async (pondId: string) => {
    setSelectedPond(pondId);
    setLoading(true);
    try {
      const { fish } = await adminApiClient.listPondFish(pondId);
      setPondFish(fish);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : '加载失败');
    } finally {
      setLoading(false);
    }
  };

  if (!ready || !authenticated) return null;

  return (
    <AppScreen>
      <AppHeader title="Debug / 管理" onBack={() => router.back()} backLabel="← 返回" />
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.hint}>
          默认密钥：fish-social-debug（生产环境请设置环境变量 ADMIN_SECRET）
        </Text>

        <View style={styles.row}>
          <TextInput
            style={styles.input}
            value={adminKey}
            onChangeText={setAdminKey}
            placeholder="管理员密钥"
            placeholderTextColor="#999"
            secureTextEntry
          />
          <Pressable style={styles.btn} onPress={handleLogin}>
            <Text style={styles.btnText}>验证</Text>
          </Pressable>
        </View>

        {message ? <Text style={styles.error}>{message}</Text> : null}
        {loading && <ActivityIndicator color={colors.primary} style={styles.loader} />}

        {authed && (
          <>
            <AdminConfigPanel />
            <AdminMetricsPanel />

            <Text style={styles.section}>鱼塘概览（数据已持久化，重启后恢复）</Text>
            {ponds.map(({ pondId, summary }) => {
              const pondName = PONDS.find((p) => p.id === pondId)?.name ?? pondId;
              return (
                <Pressable key={pondId} style={styles.card} onPress={() => loadFish(pondId)}>
                  <Text style={styles.cardTitle}>{pondName}</Text>
                  <Text style={styles.cardMeta}>
                    鱼群 {summary.fishCount}/{summary.maxPopulation}
                    {summary.depleted ? ' · 恢复中' : ''}
                  </Text>
                </Pressable>
              );
            })}

            {selectedPond && (
              <AdminPondFishDebugGrid pondId={selectedPond} fish={pondFish} />
            )}

            <Text style={styles.section}>危险操作</Text>
            <Pressable
              style={[styles.btn, styles.warnBtn]}
              onPress={async () => {
                if (!(await confirmAction('重置生态', '将清空所有鱼塘鱼并重新播种，确认？'))) return;
                const res = await adminApiClient.resetEcology();
                setMessage(res.message);
                loadAll();
              }}
            >
              <Text style={styles.btnText}>重置所有鱼塘生态</Text>
            </Pressable>
            <Pressable
              style={[styles.btn, styles.dangerBtn]}
              onPress={async () => {
                if (!(await confirmAction('清理用户', '将删除所有玩家、背包、社交数据（鱼塘保留），确认？'))) return;
                const res = await adminApiClient.clearUsers();
                setMessage(res.message);
                loadAll();
              }}
            >
              <Text style={styles.btnText}>清理所有用户数据</Text>
            </Pressable>

            <Text style={styles.section}>服务端报错日志</Text>
            <Pressable
              style={styles.btnSecondary}
              onPress={async () => {
                await adminApiClient.clearLogs();
                setLogs([]);
              }}
            >
              <Text style={styles.btnSecondaryText}>清空日志</Text>
            </Pressable>
            {logs.length === 0 ? (
              <Text style={styles.hint}>暂无报错记录</Text>
            ) : (
              logs.map((log) => (
                <View key={log.id} style={styles.logRow}>
                  <Text style={styles.logTime}>
                    {new Date(log.createdAt).toLocaleString()}
                    {log.context ? ` · ${log.context}` : ''}
                  </Text>
                  <Text style={styles.logMsg}>{log.message}</Text>
                  {log.stack ? (
                    <Text style={styles.logStack} numberOfLines={4}>
                      {log.stack}
                    </Text>
                  ) : null}
                </View>
              ))
            )}
          </>
        )}
      </ScrollView>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.md, paddingBottom: 40 },
  hint: { fontSize: 12, color: colors.textSecondary, marginBottom: spacing.sm },
  row: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.md },
  input: {
    flex: 1,
    backgroundColor: '#fff',
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 14,
  },
  btn: {
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingHorizontal: 16,
    justifyContent: 'center',
    cursor: 'pointer',
  },
  btnText: { color: '#fff', fontWeight: '700', fontSize: 14, textAlign: 'center', paddingVertical: 10 },
  btnSecondary: {
    alignSelf: 'flex-start',
    marginBottom: spacing.sm,
    padding: 8,
    cursor: 'pointer',
  },
  btnSecondaryText: { color: colors.primary, fontWeight: '600' },
  warnBtn: { backgroundColor: '#FF9800', marginBottom: spacing.sm },
  dangerBtn: { backgroundColor: '#E57373', marginBottom: spacing.lg },
  error: { color: '#c00', marginBottom: spacing.sm },
  loader: { marginVertical: spacing.md },
  section: {
    fontSize: 16,
    fontWeight: '800',
    color: colors.primaryDark,
    marginTop: spacing.lg,
    marginBottom: spacing.sm,
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.sm,
    cursor: 'pointer',
  },
  cardTitle: { fontWeight: '700', color: '#333' },
  cardMeta: { fontSize: 12, color: '#666', marginTop: 4 },
  logRow: {
    backgroundColor: '#fff5f5',
    padding: 10,
    borderRadius: 8,
    marginBottom: 8,
  },
  logTime: { fontSize: 10, color: '#888' },
  logMsg: { fontSize: 13, color: '#c00', marginTop: 4 },
  logStack: { fontSize: 10, color: '#666', marginTop: 4, fontFamily: Platform.OS === 'web' ? 'monospace' : undefined },
});

import { useCallback, useEffect, useState } from 'react';
import { View, Text, StyleSheet, Pressable, ActivityIndicator, TextInput, ScrollView } from 'react-native';
import type { FishingMetricsSummary, PlayerFishingTimeline } from '@fish-social/shared';
import { adminApiClient } from '../lib/adminApi';
import { colors, spacing, radius } from '../lib/theme';

function formatTime(ms: number): string {
  return new Date(ms).toLocaleString();
}

function payloadSummary(payload: Record<string, unknown>): string {
  const parts: string[] = [];
  if (payload.reason) parts.push(`reason=${String(payload.reason)}`);
  if (payload.spotId) parts.push(`spot=${String(payload.spotId)}`);
  if (payload.fishingPhase) parts.push(`phase=${String(payload.fishingPhase)}`);
  if (payload.speciesId) parts.push(`species=${String(payload.speciesId)}`);
  return parts.join(' · ') || JSON.stringify(payload).slice(0, 80);
}

export function AdminMetricsPanel() {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<FishingMetricsSummary | null>(null);
  const [playerId, setPlayerId] = useState('');
  const [timelineLoading, setTimelineLoading] = useState(false);
  const [timeline, setTimeline] = useState<PlayerFishingTimeline | null>(null);
  const [healthLoading, setHealthLoading] = useState(false);
  const [health, setHealth] = useState<Awaited<ReturnType<typeof adminApiClient.getBusinessHealth>> | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const summary = await adminApiClient.getFishingMetrics(168);
      setData(summary);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadTimeline = useCallback(async () => {
    const id = playerId.trim();
    if (!id) return;
    setTimelineLoading(true);
    try {
      const result = await adminApiClient.getPlayerFishingTimeline(id, 24, 500);
      setTimeline(result);
    } finally {
      setTimelineLoading(false);
    }
  }, [playerId]);

  const loadHealth = useCallback(async () => {
    setHealthLoading(true);
    try {
      const trend = await adminApiClient.getBusinessHealth(7);
      setHealth(trend);
    } finally {
      setHealthLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      load();
      loadHealth();
    }
  }, [open, load, loadHealth]);

  return (
    <View style={styles.wrap}>
      <Pressable style={styles.header} onPress={() => setOpen((v) => !v)}>
        <Text style={styles.headerTitle}>钓鱼指标 (C7) {open ? '▾' : '▸'}</Text>
      </Pressable>
      {open && (
        <View style={styles.body}>
          {loading ? <ActivityIndicator color={colors.primary} /> : null}
          {data && (
            <>
              <Text style={styles.row}>统计窗口：{data.periodHours}h</Text>
              <Text style={styles.row}>上钩 {data.catchCount} · 脱钩 {data.escapeCount}</Text>
              <Text style={styles.row}>
                弃钓率 {(data.abandonRate * 100).toFixed(1)}% · 连续脱钩玩家 {data.escapeStreakPlayers}
              </Text>
              <Text style={styles.row}>
                金币 faucet≈{data.faucetCoinsEstimate} · sink≈{data.sinkCoinsEstimate}
              </Text>
              {data.alerts.map((a) => (
                <Text key={a} style={styles.alert}>
                  ⚠ {a}
                </Text>
              ))}
            </>
          )}

          <View style={styles.timelineSection}>
            <Text style={styles.sectionTitle}>业务健康看板（7 日）</Text>
            {healthLoading ? <ActivityIndicator color={colors.primary} /> : null}
            {health && (
              <>
                <Text style={styles.row}>
                  {health.fromDate} ~ {health.toDate} · 上钩 {health.totals.catchCount} · 断线{' '}
                  {health.totals.disconnectCount}
                </Text>
                <Text style={styles.row}>
                  bite hit {health.totals.biteTickHit} / miss {health.totals.biteTickMiss} · 活跃玩家{' '}
                  {health.totals.activePlayers}
                </Text>
                {health.daily.slice(-3).map((day) => (
                  <Text key={day.dateKey} style={styles.row}>
                    {day.dateKey}: 钓获 {day.totalCatch} · 断线率 {(day.disconnectRate * 100).toFixed(1)}% ·
                    获鱼率 {(day.biteHitRate * 100).toFixed(1)}% · 塘 {day.ponds.length}
                  </Text>
                ))}
              </>
            )}
          </View>

          <View style={styles.timelineSection}>
            <Text style={styles.sectionTitle}>玩家时间线（挂机排查）</Text>
            <View style={styles.timelineRow}>
              <TextInput
                style={styles.input}
                value={playerId}
                onChangeText={setPlayerId}
                placeholder="player_id"
                autoCapitalize="none"
              />
              <Pressable style={styles.queryBtn} onPress={loadTimeline} disabled={timelineLoading}>
                <Text style={styles.queryBtnText}>{timelineLoading ? '...' : '查询'}</Text>
              </Pressable>
            </View>
            {timeline && (
              <>
                <Text style={styles.row}>
                  disconnect {timeline.summary.disconnectCount} · reconnect {timeline.summary.reconnectCount}
                  {' · '}timeout {timeline.summary.disconnectTimeoutCount} · leave {timeline.summary.leavePondCount}
                </Text>
                <ScrollView style={styles.timelineScroll} nestedScrollEnabled>
                  {timeline.events.map((event) => (
                    <View key={event.id} style={styles.eventRow}>
                      <Text style={styles.eventTime}>{formatTime(event.createdAt)}</Text>
                      <Text style={styles.eventType}>{event.eventType}</Text>
                      <Text style={styles.eventPayload} numberOfLines={2}>
                        {event.pondId ?? '-'} · {payloadSummary(event.payload)}
                      </Text>
                    </View>
                  ))}
                </ScrollView>
              </>
            )}
          </View>
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
  row: { fontSize: 13, color: '#333', marginBottom: 4 },
  alert: { fontSize: 12, color: '#c62828', marginTop: 4 },
  timelineSection: { marginTop: spacing.md, borderTopWidth: 1, borderTopColor: '#eee', paddingTop: spacing.md },
  sectionTitle: { fontWeight: '700', fontSize: 13, color: colors.primaryDark, marginBottom: spacing.sm },
  timelineRow: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center', marginBottom: spacing.sm },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm,
    paddingVertical: 6,
    fontSize: 13,
  },
  queryBtn: {
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.md,
    paddingVertical: 8,
  },
  queryBtnText: { color: '#fff', fontWeight: '600', fontSize: 12 },
  timelineScroll: { maxHeight: 280 },
  eventRow: { borderBottomWidth: 1, borderBottomColor: '#f0f0f0', paddingVertical: 6 },
  eventTime: { fontSize: 10, color: '#888' },
  eventType: { fontSize: 12, fontWeight: '700', color: '#333' },
  eventPayload: { fontSize: 11, color: '#555' },
});

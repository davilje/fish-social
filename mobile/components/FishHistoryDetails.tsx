import { useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import {
  formatFishHistorySummary, formatFishHistoryTime, formatFishSize, getPondById,
  type FishHistorySnapshot,
} from '@fish-social/shared';

export function FishHistoryDetails({ history }: { history?: FishHistorySnapshot | null }) {
  const [expanded, setExpanded] = useState(false);
  return (
    <View style={styles.container}>
      <Text style={styles.summary}>{formatFishHistorySummary(history)}</Text>
      {history && <>
        <Text style={styles.summary}>
          吃饵{history.hookFeedCount + history.groundbaitFeedCount}次 · 脱钩{history.escapeCount}次
        </Text>
        <Pressable accessibilityRole="button" accessibilityState={{ expanded }} onPress={() => setExpanded(!expanded)}>
          <Text style={styles.link}>{expanded ? '收起履历' : '查看这条鱼的履历'}</Text>
        </Pressable>
        {expanded && <ScrollView style={styles.events} nestedScrollEnabled>
          <Text style={styles.event}>入塘：{formatFishHistoryTime(history.enteredAt)}</Text>
          <Text style={styles.event}>钓获：{formatFishHistoryTime(history.caughtAt)}</Text>
          <Text style={styles.event}>钩饵{history.hookFeedCount}次 · 窝料{history.groundbaitFeedCount}份</Text>
          {history.escapes.map((event, index) => <Text key={`${event.at}:${index}`} style={styles.event}>
            {formatFishHistoryTime(event.at)} · {getPondById(event.pondId)?.name ?? event.pondId} · {event.spotId} · {formatFishSize(event.sizeM)}时脱钩
          </Text>)}
          {history.escapeCount === 0 && <Text style={styles.event}>没有脱钩记录</Text>}
        </ScrollView>}
      </>}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignSelf: 'stretch', backgroundColor: '#f1f5ee', borderRadius: 10, padding: 10, marginVertical: 10 },
  summary: { fontSize: 12, color: '#476252', lineHeight: 19 },
  link: { color: '#2C5F6F', fontSize: 12, paddingVertical: 6, fontWeight: '600' },
  events: { maxHeight: 150 },
  event: { fontSize: 11, lineHeight: 18, color: '#5c685e', marginBottom: 3 },
});

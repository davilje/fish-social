import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';
import { formatFishSize, getSpecies, type AlbumCard } from '@fish-social/shared';
import { socialApi } from '../lib/socialApi';
import { FishHistoryDetails } from './FishHistoryDetails';

/** The album retains the captured snapshot even after a fish is sold or returned. */
export function FishAlbum({ playerId }: { playerId: string }) {
  const [cards, setCards] = useState<AlbumCard[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true;
    setLoading(true);
    socialApi.getProfileHub(playerId).then(({ hub }) => {
      if (!current) return;
      const unique = new Map<string, AlbumCard>();
      for (const card of [...hub.albumPins, ...hub.albumCandidates]) unique.set(card.id, card);
      setCards([...unique.values()]);
      setError('');
    }).catch(() => { if (current) setError('暂时无法加载相册'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [playerId, revision]);
  return <View style={styles.section}>
    <Text style={styles.title}>鱼获相册</Text>
    {loading ? <ActivityIndicator /> : error ? <Text>{error}</Text> : cards.length === 0 ?
      <Text style={styles.muted}>新的鱼获故事会在这里留下纪念。</Text> : cards.map(card =>
        <View key={card.id} style={styles.card}>
          <Text style={styles.name}>{getSpecies(card.speciesId).name} · {formatFishSize(card.sizeM)}</Text>
          <Text style={styles.muted}>{card.pondName ?? card.pondId ?? '来源鱼塘未记录'}</Text>
          <FishHistoryDetails key={card.id} history={card.history} />
        </View>)}
    <Pressable onPress={() => setRevision(revision + 1)} disabled={loading}>
      <Text style={styles.refresh}>刷新相册</Text>
    </Pressable>
  </View>;
}
const styles = StyleSheet.create({
  section: { marginVertical: 18 }, title: { fontSize: 18, fontWeight: '700', color: '#2C5F6F', marginBottom: 12 },
  card: { padding: 14, borderRadius: 12, backgroundColor: '#fff', marginBottom: 10 },
  name: { fontSize: 15, fontWeight: '600', color: '#304b40' }, muted: { fontSize: 12, color: '#6c7b72', marginTop: 5 },
  refresh: { fontSize: 13, color: '#2C5F6F', paddingVertical: 8 },
});

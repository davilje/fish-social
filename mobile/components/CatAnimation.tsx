import { useEffect, useMemo, useState } from 'react';
import { Image, View } from 'react-native';
import { resolveCatSkinId, type PondUser } from '@fish-social/shared';
import catalog from '../assets/cats/catalog.json';
import { CatAnimationPlayer } from '../lib/catAnimation';
import { resolveAssetUrl } from '../lib/avatarUrl';

export function CatAnimation({ user, faceLeft }: { user: PondUser; faceLeft: boolean }) {
  const skin = resolveCatSkinId(user.avatarUrl, user.playerId ?? user.id);
  const player = useMemo(() => new CatAnimationPlayer(), [skin]);
  const [snapshot, setSnapshot] = useState({ clip: 'idle', frame: 0 });
  const [loaded, setLoaded] = useState('');
  useEffect(() => {
    player.request(user.fishingPhase, user.phaseContext?.outcome);
    setSnapshot({ clip: player.clip, frame: player.frame });
  }, [player, user.fishingPhase, user.phaseContext?.outcome]);
  const uri = resolveAssetUrl(`/image/cats/${skin}/${snapshot.clip}.png`)!;
  useEffect(() => {
    if (loaded !== uri) return;
    let previous = Date.now();
    const timer = setInterval(() => {
      const now = Date.now();
      player.advance(now - previous);
      previous = now;
      setSnapshot(current => current.clip === player.clip && current.frame === player.frame
        ? current : { clip: player.clip, frame: player.frame });
    }, 40);
    return () => clearInterval(timer);
  }, [player, uri, loaded]);
  const width = 120;
  const height = width * catalog.frameHeight / catalog.frameWidth;
  const left = 36 - catalog.foot[0] / 640 * width;
  const top = 48 - catalog.foot[1] / 512 * height;
  const definition = catalog.clips[snapshot.clip as keyof typeof catalog.clips];
  return (
    <View pointerEvents="none" style={{ width: 72, height: 48, transform: [{ scaleX: faceLeft ? -1 : 1 }] }}>
      {loaded !== uri && <Image source={{ uri: resolveAssetUrl(`/image/profile/cat_avatar_${skin}.png`) }}
        style={{ position: 'absolute', left: 11, top: 0, width: 50, height: 48 }} resizeMode="contain" />}
      <View style={{ position: 'absolute', left, top, width, height, overflow: 'hidden' }}>
        <Image source={{ uri }} onLoad={() => setLoaded(uri)}
          style={{ width: width * definition.count, height, opacity: loaded === uri ? 1 : 0,
            transform: [{ translateX: -snapshot.frame * width }] }} resizeMode="stretch" />
      </View>
    </View>
  );
}

import { useEffect, useRef, useState } from 'react';
import { View, Text, Modal, Pressable, StyleSheet, Animated } from 'react-native';
import type { FishingPrompt } from '@fish-social/shared';
import {
  FISHING_PROMPT_AUTO_CLOSE_MS,
  formatFishSize,
  getQualityInfo,
  getSpecies,
} from '@fish-social/shared';
import { formatDuration } from '../lib/config';

interface Props {
  prompt: FishingPrompt | null;
  loading?: boolean;
  onConfirm: () => void;
}

const AUTO_CLOSE_SEC = Math.ceil(FISHING_PROMPT_AUTO_CLOSE_MS / 1000);

const MISS_COPY = {
  empty: { title: '空军！', text: '什么也没钓到，下次加油' },
  escaped: { title: '脱钩了！', text: '鱼儿挣脱了鱼钩，溜走了' },
} as const;

function CodexNewBadge() {
  const badgeScale = useRef(new Animated.Value(0.6)).current;

  useEffect(() => {
    badgeScale.setValue(0.6);
    Animated.timing(badgeScale, {
      toValue: 1,
      duration: 200,
      useNativeDriver: true,
    }).start();
  }, [badgeScale]);

  return (
    <Animated.View style={[styles.codexBadge, { transform: [{ scale: badgeScale }] }]}>
      <Text style={styles.codexBadgeText}>新</Text>
    </Animated.View>
  );
}

export function CatchFishModal({ prompt, loading, onConfirm }: Props) {
  const [countdown, setCountdown] = useState(AUTO_CLOSE_SEC);
  const onConfirmRef = useRef(onConfirm);
  onConfirmRef.current = onConfirm;

  useEffect(() => {
    if (!prompt) return;

    setCountdown(AUTO_CLOSE_SEC);
    const deadline = Date.now() + FISHING_PROMPT_AUTO_CLOSE_MS;
    const tick = setInterval(() => {
      const left = Math.max(0, Math.ceil((deadline - Date.now()) / 1000));
      setCountdown(left);
      if (left <= 0) clearInterval(tick);
    }, 200);

    const timer = setTimeout(() => {
      onConfirmRef.current();
    }, FISHING_PROMPT_AUTO_CLOSE_MS);

    return () => {
      clearTimeout(timer);
      clearInterval(tick);
    };
  }, [prompt]);

  if (!prompt) return null;

  if (prompt.kind === 'miss') {
    const copy = MISS_COPY[prompt.data.reason];
    return (
      <Modal visible transparent animationType="slide" onRequestClose={onConfirm}>
        <View style={styles.overlay}>
          <View style={[styles.card, styles.missCard]}>
            <Text style={styles.missTitle}>{copy.title}</Text>
            <Text style={styles.missIcon}>💨</Text>
            <Text style={styles.missText}>{copy.text}</Text>
            <Text style={styles.countdown}>{countdown} 秒后自动关闭</Text>
            <Pressable style={[styles.btn, styles.missBtn]} onPress={onConfirm} disabled={loading}>
              <Text style={styles.btnText}>{loading ? '...' : '知道了'}</Text>
            </Pressable>
          </View>
        </View>
      </Modal>
    );
  }

  const species = getSpecies(prompt.data.speciesId);
  const quality = getQualityInfo(prompt.data.quality);
  const isCodexNew = prompt.data.isCodexNew === true;
  const hookMs = prompt.data.hookDurationMs;
  const hookHint =
    hookMs && hookMs >= 3600_000 ? `收杆等待：${formatDuration(hookMs)}` : null;

  return (
    <Modal visible transparent animationType="slide" onRequestClose={onConfirm}>
      <View style={styles.overlay}>
        <View style={[styles.card, { borderColor: quality.color }]}>
          <Text style={styles.hookTitle}>🎣 鱼上钩了！</Text>
          <View style={styles.iconWrapOuter}>
            <View style={[styles.iconWrap, { backgroundColor: quality.color + '22' }]}>
              <Text style={styles.icon}>{species.icon}</Text>
            </View>
            {isCodexNew ? <CodexNewBadge /> : null}
          </View>
          <Text style={styles.species}>{species.name}</Text>
          <Text style={[styles.quality, { color: quality.color }]}>
            【{quality.name}】
          </Text>
          {isCodexNew ? <Text style={styles.codexHint}>首次收录图鉴</Text> : null}
          <Text style={styles.size}>尺寸：{formatFishSize(prompt.data.sizeM)}</Text>
          {hookHint ? <Text style={styles.hookHint}>{hookHint}</Text> : null}
          <Text style={styles.desc}>
            品类：{species.name}{'\n'}
            品质：{quality.name}{'\n'}
            体长：{formatFishSize(prompt.data.sizeM)}
          </Text>
          <Text style={styles.countdown}>{countdown} 秒后自动收入背包</Text>
          <Pressable
            style={[styles.btn, { backgroundColor: quality.color }]}
            onPress={onConfirm}
            disabled={loading}
          >
            <Text style={styles.btnText}>{loading ? '...' : '获得'}</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.55)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  card: {
    width: '100%',
    maxWidth: 320,
    backgroundColor: '#fff',
    borderRadius: 20,
    padding: 24,
    alignItems: 'center',
    borderWidth: 3,
  },
  missCard: {
    borderColor: '#9E9E9E',
  },
  hookTitle: {
    fontSize: 20,
    fontWeight: '800',
    color: '#2C5F6F',
    marginBottom: 16,
  },
  missTitle: {
    fontSize: 22,
    fontWeight: '800',
    color: '#666',
    marginBottom: 12,
  },
  missIcon: {
    fontSize: 48,
    marginBottom: 12,
  },
  missText: {
    fontSize: 15,
    color: '#666',
    textAlign: 'center',
    lineHeight: 22,
    marginBottom: 12,
  },
  iconWrapOuter: {
    position: 'relative',
    marginBottom: 12,
  },
  iconWrap: {
    width: 88,
    height: 88,
    borderRadius: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  codexBadge: {
    position: 'absolute',
    top: -4,
    right: -4,
    backgroundColor: '#F44336',
    borderRadius: 8,
    minWidth: 22,
    paddingHorizontal: 6,
    paddingVertical: 2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  codexBadgeText: {
    color: '#fff',
    fontSize: 11,
    fontWeight: '800',
  },
  codexHint: {
    fontSize: 12,
    color: '#888',
    marginTop: 4,
  },
  hookHint: {
    fontSize: 13,
    color: '#2C5F6F',
    fontWeight: '600',
    marginTop: 6,
  },
  icon: {
    fontSize: 44,
  },
  species: {
    fontSize: 22,
    fontWeight: '700',
    color: '#333',
  },
  quality: {
    fontSize: 16,
    fontWeight: '700',
    marginTop: 4,
  },
  size: {
    fontSize: 14,
    color: '#666',
    marginTop: 8,
  },
  desc: {
    fontSize: 13,
    color: '#888',
    textAlign: 'center',
    lineHeight: 22,
    marginTop: 12,
    marginBottom: 12,
  },
  countdown: {
    fontSize: 12,
    color: '#999',
    marginBottom: 16,
  },
  btn: {
    borderRadius: 24,
    paddingHorizontal: 48,
    paddingVertical: 12,
    cursor: 'pointer',
  },
  missBtn: {
    backgroundColor: '#9E9E9E',
  },
  btnText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '700',
  },
});

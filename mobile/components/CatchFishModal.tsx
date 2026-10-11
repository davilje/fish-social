import { useEffect, useRef, useState } from 'react';
import { View, Text, Modal, Pressable, StyleSheet } from 'react-native';
import type { FishingPrompt } from '@fish-social/shared';
import {
  FISHING_PROMPT_AUTO_CLOSE_MS,
  calcFishWeightKg,
  formatFishSize,
  formatFishWeight,
} from '@fish-social/shared';

interface Props {
  prompt: FishingPrompt | null;
  loading?: boolean;
  onConfirm: () => void;
}

const AUTO_CLOSE_SEC = Math.ceil(FISHING_PROMPT_AUTO_CLOSE_MS / 1000);

const MISS_COPY = {
  empty: { title: '空军！', text: '什么也没钓到，下次加油' },
  escaped: { title: '脱钩了！', text: '鱼儿挣脱了鱼钩，溜走了' },
  nobite: { title: '空军！', text: '什么也没钓到，下次加油' },
} as const;

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
    const copy = MISS_COPY[prompt.data.reason] ?? MISS_COPY.escaped;
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

  const sizeLine = `尺寸：${formatFishSize(prompt.data.sizeM)}`;
  const weightLine = `体重：${formatFishWeight(calcFishWeightKg(prompt.data.sizeM))}`;
  const returned = prompt.data.autoReturned === true && (prompt.data.gold ?? 0) > 0;
  const hint = returned
    ? `回鱼价格：${prompt.data.gold} 金币`
    : '已放入背包';

  return (
    <Modal visible transparent animationType="slide" onRequestClose={onConfirm}>
      <View style={styles.overlay}>
        <View style={styles.card}>
          <Text style={styles.hookTitle}>钓到了！</Text>
          <Text style={styles.desc}>
            {sizeLine}
            {'\n'}
            {weightLine}
            {'\n'}
            {hint}
          </Text>
          <Text style={styles.countdown}>{countdown} 秒后自动关闭</Text>
          <Pressable style={styles.btn} onPress={onConfirm} disabled={loading}>
            <Text style={styles.btnText}>{loading ? '...' : '知道了'}</Text>
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
    borderColor: '#2C5F6F',
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
  desc: {
    fontSize: 15,
    color: '#333',
    textAlign: 'center',
    lineHeight: 24,
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
    backgroundColor: '#2C5F6F',
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

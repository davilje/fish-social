import { useEffect, useRef, useState } from 'react';
import { Pressable, View, Text, StyleSheet, Platform } from 'react-native';
import { type PondUser, type FishingPhase } from '@fish-social/shared';
import { ProfileAvatar } from './ProfileAvatar';
import { HookCountdownRing } from './HookCountdownRing';
import { CatAnimation } from './CatAnimation';

const AVATAR = 28;
const HOVER_DELAY_MS = 300;
const BUBBLE_TIMEOUT_MS = 3500;

type StatusKind = 'hooked' | 'fishing' | null;

function resolveStatusKind(phase?: FishingPhase, outcome?: string): StatusKind {
  if (phase === 'hooked') return 'hooked';
  if (phase === 'resolving' && outcome === 'catch') return 'hooked';
  if (
    phase === 'waiting' ||
    phase === 'baiting' ||
    phase === 'casting' ||
    phase === 'resolving' ||
    phase === 'stopping'
  ) {
    return 'fishing';
  }
  return null;
}

interface Props {
  user: PondUser;
  isMe: boolean;
  faceLeft: boolean;
  onPress?: () => void;
  /** 他人气泡开关（视觉在 PondScene Overlay） */
  onBubbleChange?: (userId: string, visible: boolean) => void;
}

/** 二头身角色：交互在此；气泡/飘字由场景 Overlay 绘制 */
export function PondCharacter({ user, isMe, faceLeft, onPress, onBubbleChange }: Props) {
  const phase: FishingPhase | undefined = user.fishingPhase;
  const statusKind = resolveStatusKind(phase, user.phaseContext?.outcome);
  const [now, setNow] = useState(Date.now());
  const [bubbleVisible, setBubbleVisible] = useState(false);
  const [hookTotalMs, setHookTotalMs] = useState(0);
  const hoverTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const bubbleTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const onBubbleChangeRef = useRef(onBubbleChange);
  onBubbleChangeRef.current = onBubbleChange;

  const showHookRing = phase === 'hooked' && !!user.phaseEndsAt;

  useEffect(() => {
    if (!showHookRing) return;
    const tick = setInterval(() => setNow(Date.now()), 250);
    return () => clearInterval(tick);
  }, [showHookRing]);

  useEffect(() => {
    if (phase === 'hooked' && user.phaseEndsAt) {
      const rem = Math.max(0, user.phaseEndsAt - Date.now());
      setHookTotalMs((prev) => (prev > 0 ? Math.max(prev, rem) : Math.max(rem, 1000)));
    } else {
      setHookTotalMs(0);
    }
  }, [phase, user.phaseEndsAt]);

  useEffect(() => {
    return () => {
      if (hoverTimer.current) clearTimeout(hoverTimer.current);
      if (bubbleTimer.current) clearTimeout(bubbleTimer.current);
    };
  }, []);

  useEffect(() => {
    if (statusKind == null && bubbleVisible) {
      setBubbleVisible(false);
    }
  }, [statusKind, bubbleVisible]);

  useEffect(() => {
    onBubbleChangeRef.current?.(user.id, bubbleVisible);
  }, [bubbleVisible, user.id]);

  useEffect(() => {
    return () => {
      onBubbleChangeRef.current?.(user.id, false);
    };
  }, [user.id]);

  const hookRemainingMs =
    phase === 'hooked' && user.phaseEndsAt ? Math.max(0, user.phaseEndsAt - now) : 0;
  const hookProgress =
    showHookRing && hookTotalMs > 0 ? Math.max(0, Math.min(1, hookRemainingMs / hookTotalMs)) : 0;

  const disconnected = phase === 'disconnected';

  const clearBubbleTimers = () => {
    if (hoverTimer.current) {
      clearTimeout(hoverTimer.current);
      hoverTimer.current = null;
    }
    if (bubbleTimer.current) {
      clearTimeout(bubbleTimer.current);
      bubbleTimer.current = null;
    }
  };

  const openBubble = () => {
    if (isMe || statusKind == null) return;
    setBubbleVisible(true);
    if (bubbleTimer.current) clearTimeout(bubbleTimer.current);
    bubbleTimer.current = setTimeout(() => setBubbleVisible(false), BUBBLE_TIMEOUT_MS);
  };

  const onHoverIn = () => {
    if (isMe || statusKind == null || Platform.OS !== 'web') return;
    clearBubbleTimers();
    hoverTimer.current = setTimeout(() => setBubbleVisible(true), HOVER_DELAY_MS);
  };

  const onHoverOut = () => {
    if (Platform.OS !== 'web') return;
    clearBubbleTimers();
    setBubbleVisible(false);
  };

  const handlePress = () => {
    if (!isMe && statusKind != null) {
      openBubble();
    }
    onPress?.();
  };

  const avatarNode = (
    <View style={[styles.avatarFloat, isMe && styles.avatarFloatMe]}>
      <ProfileAvatar nickname={user.nickname} avatarUrl={user.avatarUrl} size={AVATAR} />
    </View>
  );

  return (
    <View style={[styles.wrap, disconnected && styles.wrapDisconnected]}>
      {showHookRing ? (
        <View style={styles.ringSlot}>
          <HookCountdownRing progress={hookProgress} size={44} />
        </View>
      ) : null}

      <Pressable
        onPress={handlePress}
        onLongPress={!isMe ? openBubble : undefined}
        delayLongPress={280}
        style={styles.hitArea}
        disabled={!onPress && isMe}
        {...(Platform.OS === 'web'
          ? ({ onHoverIn, onHoverOut } as Record<string, unknown>)
          : {})}
      >
        {avatarNode}

        <CatAnimation user={user} faceLeft={faceLeft} />

        <Text style={[styles.nickname, disconnected && styles.nicknameDisconnected]} numberOfLines={1}>
          {user.nickname}
        </Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    alignItems: 'center',
    width: 72,
    position: 'relative',
    overflow: 'visible',
  },
  wrapDisconnected: { opacity: 0.45 },
  hitArea: {
    alignItems: 'center',
    cursor: 'pointer',
  },
  avatarFloat: {
    marginBottom: -6,
    zIndex: 3,
    borderRadius: 999,
    borderWidth: 2,
    borderColor: '#fff',
    backgroundColor: '#fff',
  },
  avatarFloatMe: {
    borderColor: '#FFD700',
    borderWidth: 2.5,
  },
  ringSlot: {
    position: 'absolute',
    top: -6,
    zIndex: 4,
  },
  nickname: {
    marginTop: 4,
    fontSize: 10,
    fontWeight: '700',
    color: '#2C3E50',
    maxWidth: 68,
    textAlign: 'center',
    textShadowColor: 'rgba(255,255,255,0.8)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
  nicknameDisconnected: { color: '#78909C' },
});

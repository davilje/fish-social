import { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TextInput,
  Pressable,
  ActivityIndicator,
  Platform,
  Alert,
} from 'react-native';
import { useRouter, useRootNavigationState } from 'expo-router';
import { ProfileAvatar } from '../components/ProfileAvatar';
import { DefaultAvatarPicker } from '../components/DefaultAvatarPicker';
import { AppScreen } from '../components/AppScreen';
import { createPlayerId, setAuthSession, isLoggedIn } from '../lib/auth';
import { fetchDevToken } from '../lib/apiClient';
import { setStoredToken } from '../lib/jwtToken';
import { socialApi } from '../lib/socialApi';
import { pickAvatarOnWeb } from '../lib/pickAvatar';
import { useResponsive } from '../lib/responsive';
import { colors, spacing, radius, shadow } from '../lib/theme';

export default function LoginScreen() {
  const router = useRouter();
  const navigationState = useRootNavigationState();
  const { isMobile, cardPadding, contentPadding } = useResponsive();
  const [nickname, setNickname] = useState('');
  const [avatarUrl, setAvatarUrl] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!navigationState?.key) return;
    if (isLoggedIn()) router.replace('/');
  }, [navigationState?.key, router]);

  const handleLogin = async () => {
    const nick = nickname.trim().slice(0, 12);
    if (!nick) {
      if (Platform.OS === 'web') window.alert('请输入昵称');
      else Alert.alert('提示', '请输入昵称');
      return;
    }

    setLoading(true);
    try {
      const requestedId = createPlayerId();
      const { profile, token } = await socialApi.register(requestedId, nick);
      const playerId = profile.playerId;
      if (token) {
        await setStoredToken(token, playerId);
      } else {
        await fetchDevToken(playerId);
      }
      let finalProfile = profile;
      if (avatarUrl) {
        const res = await socialApi.updateProfile(playerId, { avatarUrl });
        finalProfile = res.profile;
      }
      setAuthSession({
        playerId,
        nickname: finalProfile.nickname,
        avatarUrl: finalProfile.avatarUrl,
        loggedIn: true,
      });
      router.replace('/');
    } catch (e) {
      const msg = e instanceof Error ? e.message : '登录失败';
      if (Platform.OS === 'web') window.alert(msg);
      else Alert.alert('登录失败', msg);
    } finally {
      setLoading(false);
    }
  };

  const handlePickAvatar = async () => {
    if (Platform.OS !== 'web') return;
    try {
      const url = await pickAvatarOnWeb();
      if (url) setAvatarUrl(url);
    } catch (e) {
      const msg = e instanceof Error ? e.message : '选择头像失败';
      window.alert(msg);
    }
  };

  return (
    <AppScreen backgroundColor={colors.bgCool} scroll contentStyle={styles.scrollWrap}>
      <View style={[styles.card, { padding: cardPadding, marginHorizontal: contentPadding }]}>
        <Text style={[styles.logo, isMobile && styles.logoMobile]}>🎣 Fish Social</Text>
        <Text style={styles.subtitle}>登录后开始你的钓鱼之旅</Text>

        <Pressable style={styles.avatarWrap} onPress={handlePickAvatar}>
          <ProfileAvatar nickname={nickname || '钓'} avatarUrl={avatarUrl} size={isMobile ? 76 : 88} />
          <Text style={styles.avatarHint}>
            {Platform.OS === 'web' ? '点击上传自定义头像（可选）' : '选择下方默认头像（可选）'}
          </Text>
        </Pressable>

        <Text style={styles.defaultTitle}>选择默认头像</Text>
        <DefaultAvatarPicker selected={avatarUrl} onSelect={setAvatarUrl} size={isMobile ? 46 : 52} />

        <Text style={styles.label}>昵称</Text>
        <TextInput
          style={styles.input}
          placeholder="输入你的昵称（最多 12 字）"
          placeholderTextColor="#aaa"
          value={nickname}
          onChangeText={setNickname}
          maxLength={12}
          autoCapitalize="none"
          returnKeyType="done"
        />

        <Pressable
          style={[styles.btn, loading && styles.btnDisabled]}
          onPress={handleLogin}
          disabled={loading}
        >
          {loading ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.btnText}>进入游戏</Text>
          )}
        </Pressable>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  scrollWrap: {
    justifyContent: 'center',
    paddingVertical: spacing.lg,
  },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.xl,
    alignItems: 'center',
    maxWidth: 440,
    width: '100%',
    alignSelf: 'center',
    ...shadow.card,
  },
  logo: {
    fontSize: 28,
    fontWeight: '800',
    color: colors.primaryDark,
  },
  logoMobile: {
    fontSize: 24,
  },
  subtitle: {
    marginTop: spacing.sm,
    color: colors.textMuted,
    fontSize: 14,
    textAlign: 'center',
  },
  avatarWrap: {
    alignItems: 'center',
    marginTop: spacing.lg,
    marginBottom: spacing.sm,
    cursor: 'pointer',
  },
  avatarHint: {
    marginTop: spacing.sm,
    fontSize: 12,
    color: colors.primary,
    textAlign: 'center',
    paddingHorizontal: spacing.md,
  },
  defaultTitle: {
    marginTop: spacing.md,
    marginBottom: spacing.sm,
    fontSize: 13,
    fontWeight: '600',
    color: colors.textSecondary,
  },
  label: {
    alignSelf: 'stretch',
    marginTop: spacing.md,
    marginBottom: 6,
    color: colors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
  },
  input: {
    alignSelf: 'stretch',
    borderWidth: 1,
    borderColor: colors.borderLight,
    borderRadius: radius.md,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
    color: colors.text,
    backgroundColor: colors.surfaceMuted,
    minHeight: 48,
  },
  btn: {
    alignSelf: 'stretch',
    marginTop: spacing.lg,
    backgroundColor: colors.primary,
    borderRadius: radius.md,
    paddingVertical: 14,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    cursor: 'pointer',
  },
  btnDisabled: {
    opacity: 0.7,
  },
  btnText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '700',
  },
});

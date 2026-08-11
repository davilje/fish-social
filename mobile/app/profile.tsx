import { useMemo, useState } from 'react';
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
import { useRouter } from 'expo-router';
import type { FishInventoryItem } from '@fish-social/shared';
import { SHOWCASE_SLOT_COUNT, formatFishSize, getQualityInfo, getSpecies } from '@fish-social/shared';
import { ProfileAvatar } from '../components/ProfileAvatar';
import { DefaultAvatarPicker } from '../components/DefaultAvatarPicker';
import { AppScreen } from '../components/AppScreen';
import { AppHeader } from '../components/AppHeader';
import { AdminDebugButton } from '../components/AdminDebugButton';
import { updateAuthSession, clearAuthSession } from '../lib/auth';
import { getNickname } from '../lib/config';
import { getPlayerId } from '../lib/playerId';
import { pickAvatarOnWeb } from '../lib/pickAvatar';
import { socialApi } from '../lib/socialApi';
import { useInventory } from '../lib/useInventory';
import { useProfile } from '../lib/useProfile';
import { useRequireAuth } from '../lib/useRequireAuth';
import { useResponsive } from '../lib/responsive';
import { colors, spacing, radius } from '../lib/theme';

function toast(title: string, msg: string) {
  if (Platform.OS === 'web') window.alert(`${title}\n${msg}`);
  else Alert.alert(title, msg);
}

export default function ProfileScreen() {
  const router = useRouter();
  const { ready, authenticated, session } = useRequireAuth();
  const playerId = useMemo(() => getPlayerId(), []);
  const nickname = useMemo(() => getNickname(), []);
  const { profile, setProfile, refresh } = useProfile(playerId, nickname);
  const { isMobile, contentPadding } = useResponsive();
  const { items } = useInventory(playerId);

  const [editNick, setEditNick] = useState('');
  const [editBio, setEditBio] = useState('');
  const [saving, setSaving] = useState(false);
  const [pickingSlot, setPickingSlot] = useState<number | null>(null);

  const showcaseSlots = profile?.showcaseFishIds ?? Array(SHOWCASE_SLOT_COUNT).fill(null);

  const fishById = useMemo(() => {
    const map = new Map<string, FishInventoryItem>();
    items.forEach((f) => map.set(f.id, f));
    return map;
  }, [items]);

  const startEdit = () => {
    setEditNick(profile?.nickname ?? nickname);
    setEditBio(profile?.bio ?? '');
  };

  const saveProfile = async () => {
    setSaving(true);
    try {
      const res = await socialApi.updateProfile(playerId, {
        nickname: editNick.trim() || profile?.nickname,
        bio: editBio.trim(),
      });
      setProfile(res.profile);
      updateAuthSession({
        nickname: res.profile.nickname,
        avatarUrl: res.profile.avatarUrl,
      });
      toast('保存成功', '个人资料已更新');
    } catch (e) {
      toast('保存失败', e instanceof Error ? e.message : '');
    } finally {
      setSaving(false);
    }
  };

  const handleAvatar = async () => {
    if (Platform.OS !== 'web') return;
    try {
      const url = await pickAvatarOnWeb();
      if (!url) return;
      const res = await socialApi.updateProfile(playerId, { avatarUrl: url });
      setProfile(res.profile);
      updateAuthSession({ avatarUrl: res.profile.avatarUrl });
    } catch (e) {
      toast('上传失败', e instanceof Error ? e.message : '');
    }
  };

  const handleSelectDefaultAvatar = async (avatarPath: string) => {
    try {
      const res = await socialApi.updateProfile(playerId, { avatarUrl: avatarPath });
      setProfile(res.profile);
      updateAuthSession({ avatarUrl: res.profile.avatarUrl });
    } catch (e) {
      toast('设置失败', e instanceof Error ? e.message : '');
    }
  };

  const setSlotFish = async (slotIndex: number, fishId: string | null) => {
    const next = [...showcaseSlots];
    while (next.length < SHOWCASE_SLOT_COUNT) next.push(null);
    next[slotIndex] = fishId;
    try {
      const res = await socialApi.setShowcase(playerId, next);
      setProfile(res.profile);
      setPickingSlot(null);
    } catch (e) {
      toast('设置失败', e instanceof Error ? e.message : '');
    }
  };

  const logout = () => {
    clearAuthSession();
    router.replace('/login');
  };

  if (!ready || !authenticated || !session) return null;

  return (
    <AppScreen scroll contentStyle={{ paddingBottom: spacing.xl }}>
      <AppHeader
        title="个人信息"
        onBack={() => router.back()}
        right={
          <>
            <AdminDebugButton compact={isMobile} />
            <Pressable onPress={logout} hitSlop={8}>
              <Text style={styles.logout}>退出</Text>
            </Pressable>
          </>
        }
      />

      <View style={[styles.scroll, { paddingHorizontal: contentPadding }]}>
          <View style={styles.hero}>
            <Pressable onPress={handleAvatar}>
              <ProfileAvatar
                nickname={profile?.nickname ?? nickname}
                avatarUrl={profile?.avatarUrl ?? session.avatarUrl}
                size={isMobile ? 80 : 96}
              />
            </Pressable>
            <Text style={styles.avatarTip}>
              {Platform.OS === 'web' ? '点击上方上传自定义头像，或选择下方默认头像' : '选择下方默认头像'}
            </Text>
            <DefaultAvatarPicker
              selected={profile?.avatarUrl ?? session.avatarUrl}
              onSelect={handleSelectDefaultAvatar}
            />
            <Text style={styles.heroName}>{profile?.nickname ?? nickname}</Text>
            <Text style={styles.heroBio}>{profile?.bio || '这个人很懒，还没有写简介…'}</Text>
            <Text style={styles.coins}>💰 {profile?.coins ?? 0} 金币</Text>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>编辑资料</Text>
            <Text style={styles.label}>昵称</Text>
            <TextInput
              style={styles.input}
              value={editNick || profile?.nickname || ''}
              onChangeText={setEditNick}
              onFocus={startEdit}
              maxLength={12}
              placeholder="昵称"
            />
            <Text style={styles.label}>个人简介</Text>
            <TextInput
              style={[styles.input, styles.bioInput]}
              value={editBio || profile?.bio || ''}
              onChangeText={setEditBio}
              onFocus={startEdit}
              maxLength={120}
              multiline
              placeholder="写点什么介绍自己…"
            />
            <Pressable style={styles.saveBtn} onPress={saveProfile} disabled={saving}>
              {saving ? <ActivityIndicator color="#fff" /> : <Text style={styles.saveText}>保存</Text>}
            </Pressable>
          </View>

          <View style={styles.section}>
            <Text style={styles.sectionTitle}>收藏品（{SHOWCASE_SLOT_COUNT} 格）</Text>
            <Text style={styles.hint}>点击格子选择背包中的鱼展示，可清空</Text>
            <View style={styles.showcaseGrid}>
              {showcaseSlots.map((fishId, i) => {
                const fish = fishId ? fishById.get(fishId) : undefined;
                const species = fish ? getSpecies(fish.speciesId) : null;
                const quality = fish ? getQualityInfo(fish.quality) : null;
                return (
                  <Pressable
                    key={i}
                    style={[
                      styles.showcaseSlot,
                      pickingSlot === i && styles.showcaseSlotActive,
                      quality && { borderColor: quality.color },
                    ]}
                    onPress={() => setPickingSlot(pickingSlot === i ? null : i)}
                  >
                    {fish && species && quality ? (
                      <>
                        <Text style={styles.slotIcon}>{species.icon}</Text>
                        <Text style={[styles.slotName, { color: quality.color }]} numberOfLines={1}>
                          {species.name}
                        </Text>
                        <Text style={styles.slotSize}>{formatFishSize(fish.sizeM)}</Text>
                      </>
                    ) : (
                      <Text style={styles.slotEmpty}>+</Text>
                    )}
                  </Pressable>
                );
              })}
            </View>

            {pickingSlot !== null && (
              <View style={styles.picker}>
                <Text style={styles.pickerTitle}>选择要展示的鱼（槽位 {pickingSlot + 1}）</Text>
                <Pressable style={styles.clearSlot} onPress={() => setSlotFish(pickingSlot, null)}>
                  <Text style={styles.clearSlotText}>清空此格</Text>
                </Pressable>
                <View style={styles.pickerGrid}>
                  {items.length === 0 ? (
                    <Text style={styles.emptyFish}>背包暂无鱼获</Text>
                  ) : (
                    items.map((fish) => {
                      const species = getSpecies(fish.speciesId);
                      const quality = getQualityInfo(fish.quality);
                      return (
                        <Pressable
                          key={fish.id}
                          style={[styles.pickerItem, { borderColor: quality.color }]}
                          onPress={() => setSlotFish(pickingSlot, fish.id)}
                        >
                          <Text style={styles.pickerIcon}>{species.icon}</Text>
                          <Text style={styles.pickerLabel} numberOfLines={1}>{species.name}</Text>
                        </Pressable>
                      );
                    })
                  )}
                </View>
              </View>
            )}
          </View>

          <Pressable style={styles.refreshBtn} onPress={refresh}>
            <Text style={styles.refreshText}>刷新资料</Text>
          </Pressable>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  logout: { color: colors.danger, fontSize: 14, fontWeight: '600', cursor: 'pointer' },
  scroll: { paddingBottom: spacing.lg },
  hero: { alignItems: 'center', paddingVertical: 20 },
  avatarTip: { marginTop: 8, marginBottom: 12, fontSize: 12, color: '#888', textAlign: 'center', paddingHorizontal: 24 },
  heroName: { marginTop: 12, fontSize: 22, fontWeight: '800', color: '#2C5F6F' },
  heroBio: { marginTop: 8, fontSize: 14, color: '#666', textAlign: 'center', paddingHorizontal: 32 },
  coins: { marginTop: 8, color: '#E6A700', fontWeight: '600' },
  section: {
    marginHorizontal: 16,
    marginBottom: 16,
    backgroundColor: '#fff',
    borderRadius: 16,
    padding: 16,
  },
  sectionTitle: { fontSize: 16, fontWeight: '700', color: '#2C5F6F', marginBottom: 12 },
  label: { fontSize: 13, color: '#666', marginBottom: 6, marginTop: 8 },
  input: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 15,
    color: '#333',
    backgroundColor: '#FAFAFA',
  },
  bioInput: { minHeight: 72, textAlignVertical: 'top' },
  saveBtn: {
    marginTop: 16,
    backgroundColor: '#4A90A4',
    borderRadius: 10,
    paddingVertical: 12,
    alignItems: 'center',
    cursor: 'pointer',
  },
  saveText: { color: '#fff', fontWeight: '700' },
  hint: { fontSize: 12, color: '#999', marginBottom: 12 },
  showcaseGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  showcaseSlot: {
    width: '22%',
    minWidth: 72,
    aspectRatio: 1,
    borderWidth: 2,
    borderColor: '#ddd',
    borderRadius: 12,
    borderStyle: 'dashed',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#F8FAFB',
    padding: 4,
    cursor: 'pointer',
  },
  showcaseSlotActive: { borderColor: '#4A90A4', backgroundColor: '#E8F4F8' },
  slotEmpty: { fontSize: 24, color: '#ccc' },
  slotIcon: { fontSize: 22 },
  slotName: { fontSize: 10, fontWeight: '700', marginTop: 2 },
  slotSize: { fontSize: 9, color: '#888' },
  picker: { marginTop: 16, paddingTop: 12, borderTopWidth: 1, borderTopColor: '#eee' },
  pickerTitle: { fontSize: 14, fontWeight: '600', color: '#333', marginBottom: 8 },
  clearSlot: { alignSelf: 'flex-start', marginBottom: 8, cursor: 'pointer' },
  clearSlotText: { color: '#c62828', fontSize: 13 },
  pickerGrid: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  pickerItem: {
    width: 64,
    height: 64,
    borderWidth: 2,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#fff',
    cursor: 'pointer',
  },
  pickerIcon: { fontSize: 22 },
  pickerLabel: { fontSize: 9, color: '#666', marginTop: 2 },
  emptyFish: { color: '#aaa', padding: 12 },
  refreshBtn: { alignSelf: 'center', padding: 12, cursor: 'pointer' },
  refreshText: { color: '#4A90A4' },
});

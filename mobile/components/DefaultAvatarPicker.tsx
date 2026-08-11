import { View, Image, Pressable, StyleSheet } from 'react-native';
import { DEFAULT_AVATARS, defaultAvatarPath } from '@fish-social/shared';
import { getDefaultAvatarUrl } from '../lib/avatarUrl';

interface Props {
  selected?: string;
  onSelect: (avatarPath: string) => void;
  size?: number;
}

export function DefaultAvatarPicker({ selected, onSelect, size = 52 }: Props) {
  return (
    <View style={styles.grid}>
      {DEFAULT_AVATARS.map((avatar) => {
        const path = defaultAvatarPath(avatar.filename);
        const isSelected = selected === path;
        return (
          <Pressable
            key={avatar.id}
            onPress={() => onSelect(path)}
            style={[styles.item, isSelected && styles.itemSelected]}
            accessibilityLabel={avatar.label}
          >
            <Image
              source={{ uri: getDefaultAvatarUrl(avatar.filename) }}
              style={{ width: size, height: size, borderRadius: size / 2 }}
            />
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    gap: 12,
  },
  item: {
    borderRadius: 999,
    borderWidth: 2,
    borderColor: 'transparent',
    padding: 2,
    cursor: 'pointer',
  },
  itemSelected: {
    borderColor: '#4A90A4',
    backgroundColor: '#E8F4F8',
  },
});

import { useCallback, useState, type ReactNode, useEffect } from 'react';
import { UserProfileModal, type FriendAddedResult } from '../components/UserProfileModal';

export interface ProfileModalTarget {
  playerId: string;
  nickname: string;
  avatarUrl?: string;
}

interface Options {
  viewerPlayerId: string;
  viewerNickname: string;
  friendIds: string[];
  pendingOutgoingIds: string[];
  setFriendIds: React.Dispatch<React.SetStateAction<string[]>>;
  setPendingOutgoingIds: React.Dispatch<React.SetStateAction<string[]>>;
  refreshFriends?: () => void;
  onEditProfile?: () => void;
}

export function useProfileModal(options: Options): {
  openProfile: (target: ProfileModalTarget) => void;
  closeProfile: () => void;
  profileModal: ReactNode;
} {
  const [target, setTarget] = useState<ProfileModalTarget | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

  const openProfile = useCallback((next: ProfileModalTarget) => {
    setTarget(next);
    setModalOpen(true);
  }, []);

  const closeProfile = useCallback(() => {
    setModalOpen(false);
  }, []);

  const handleModalHidden = useCallback(() => {
    setTarget(null);
  }, []);

  useEffect(() => {
    if (!modalOpen && target) {
      const timer = setTimeout(handleModalHidden, 350);
      return () => clearTimeout(timer);
    }
  }, [modalOpen, target, handleModalHidden]);

  const handleFriendAdded = useCallback(
    (result: FriendAddedResult) => {
      if (result.becameFriend) {
        options.setFriendIds((prev) =>
          prev.includes(result.playerId) ? prev : [...prev, result.playerId],
        );
        options.setPendingOutgoingIds((prev) =>
          prev.filter((id) => id !== result.playerId),
        );
      } else {
        options.setPendingOutgoingIds((prev) =>
          prev.includes(result.playerId) ? prev : [...prev, result.playerId],
        );
      }
      options.refreshFriends?.();
    },
    [options],
  );

  const profileModal = target ? (
    <UserProfileModal
      visible={modalOpen}
      playerId={target.playerId}
      nickname={target.nickname}
      avatarUrl={target.avatarUrl}
      viewerPlayerId={options.viewerPlayerId}
      viewerNickname={options.viewerNickname}
      friendIds={options.friendIds}
      pendingOutgoingIds={options.pendingOutgoingIds}
      onClose={closeProfile}
      onModalHide={handleModalHidden}
      onFriendAdded={handleFriendAdded}
      onEditProfile={options.onEditProfile}
    />
  ) : null;

  return { openProfile, closeProfile, profileModal };
}

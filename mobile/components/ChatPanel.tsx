import { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  FlatList,
  Pressable,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import type { ChatMessage } from '@fish-social/shared';
import { spacing } from '../lib/theme';

interface Props {
  messages: ChatMessage[];
  onSend: (text: string) => Promise<{ ok: boolean; error?: string }>;
  expanded?: boolean;
  /** 嵌入 PondSocialPanel 时使用 */
  embedded?: boolean;
}

export function ChatPanel({ messages, onSend, expanded = false, embedded = false }: Props) {
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);

  const handleSend = async () => {
    if (!text.trim() || sending) return;
    setSending(true);
    const res = await onSend(text);
    if (res.ok) setText('');
    setSending(false);
  };

  return (
    <KeyboardAvoidingView
      style={[
        styles.container,
        expanded && styles.containerExpanded,
        embedded && styles.containerEmbedded,
      ]}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      {!embedded && <Text style={styles.title}>鱼塘聊天</Text>}
      <FlatList
        data={messages}
        keyExtractor={(item) => item.id}
        style={styles.list}
        renderItem={({ item }) => (
          <View style={[styles.msgRow, item.type === 'announcement' && styles.announceRow]}>
            {item.type === 'announcement' ? (
              <Text style={styles.announceText}>📢 {item.text}</Text>
            ) : (
              <>
                <Text style={styles.msgName}>{item.nickname}</Text>
                <Text style={styles.msgText}>{item.text}</Text>
              </>
            )}
          </View>
        )}
        ListEmptyComponent={
          <Text style={styles.empty}>还没有消息，打个招呼吧～</Text>
        }
      />
      <View style={styles.inputRow}>
        <TextInput
          style={styles.input}
          value={text}
          onChangeText={setText}
          placeholder="说点什么..."
          placeholderTextColor="#999"
          maxLength={200}
          onSubmitEditing={handleSend}
        />
        <Pressable
          style={({ hovered }: { hovered?: boolean }) => [
            styles.sendBtn,
            hovered && styles.sendBtnHover,
          ]}
          onPress={handleSend}
          disabled={sending}
        >
          <Text style={styles.sendText}>发送</Text>
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    height: 200,
    backgroundColor: '#fff',
    borderTopLeftRadius: 16,
    borderTopRightRadius: 16,
    paddingHorizontal: 12,
    paddingTop: 8,
  },
  containerExpanded: {
    height: '100%' as unknown as number,
    flex: 1,
    borderRadius: 12,
    margin: 12,
    marginLeft: 0,
    borderTopLeftRadius: 12,
    borderTopRightRadius: 12,
    minHeight: 400,
  },
  containerEmbedded: {
    height: '100%' as unknown as number,
    flex: 1,
    borderRadius: 0,
    margin: 0,
    paddingTop: spacing.sm,
    minHeight: 0,
  },
  title: {
    fontSize: 13,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  list: {
    flex: 1,
  },
  msgRow: {
    marginBottom: 6,
  },
  announceRow: {
    backgroundColor: '#FFF8E1',
    borderRadius: 8,
    padding: 8,
    borderLeftWidth: 3,
    borderLeftColor: '#FFB74D',
  },
  announceText: {
    fontSize: 13,
    color: '#E65100',
    fontWeight: '600',
  },
  msgName: {
    fontSize: 11,
    color: '#4A90A4',
    fontWeight: '600',
  },
  msgText: {
    fontSize: 13,
    color: '#333',
  },
  empty: {
    color: '#aaa',
    fontSize: 12,
    textAlign: 'center',
    marginTop: 20,
  },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    gap: 8,
  },
  input: {
    flex: 1,
    backgroundColor: '#f0f0f0',
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 8,
    fontSize: 14,
    color: '#333',
    outlineStyle: 'none',
  } as object,
  sendBtn: {
    backgroundColor: '#4A90A4',
    borderRadius: 20,
    paddingHorizontal: 16,
    paddingVertical: 8,
    cursor: 'pointer',
  },
  sendBtnHover: {
    backgroundColor: '#3d7a8c',
  },
  sendText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 14,
  },
});

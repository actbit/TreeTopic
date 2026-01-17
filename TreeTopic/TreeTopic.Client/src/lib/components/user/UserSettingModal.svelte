<script lang="ts">
  import { onMount } from 'svelte';
  {each} from 'svelte';
  import { goto } from '$app/navigation';
  import { api } from '$lib/api/client';
  import { ui, modals } from '$lib/stores/ui';
  import { user } from '$lib/stores/auth';
  import { currentRoom } from '$lib/stores/rooms';
  import type { RoomUserDto, ApplicationUser } from '$lib/types';

  import Button from '$lib/components/common/Button.svelte';
  import Input from '$lib/components/common/Input.svelte';
  import Modal from '$lib/components/common/Modal.svelte';

  // Props
  export let roomId: string;
  export let active: boolean = false;

  // Modal 管理用の変数
  $: modalActive = active;

  // ストアから値を取得
  const authUser = $user;
  const currentRoomData = $currentRoom;

  // UI 状態
  let isLoading = false;
  let error = '';
  let success = '';

  // タブの状態
  let activeTab = 'room'; // 'room' または 'application'

  // RoomUser 設定状態
  let roomUser: RoomUserDto = {
    id: '',
    applicationUserId: '',
    roomId: '',
    displayName: '',
    iconUrl: '',
    useMainName: false,
    useMainIcon: false
  };

  // ApplicationUser 設定状態
  let applicationUser: ApplicationUser = {
    id: '',
    tenantId: '',
    userName: '',
    normalizedUserName: '',
    email: '',
    normalizedEmail: '',
    emailConfirmed: false,
    passwordHash: '',
    securityStamp: '',
    concurrencyStamp: '',
    phoneNumber: '',
    phoneNumberConfirmed: false,
    twoFactorEnabled: false,
    lockoutEnd: null,
    lockoutEnabled: false,
    accessFailedCount: 0,
    displayName: '',
    iconFileName: '',
    sub: ''
  };

  // ファイルアップロード用
  let iconFile: File | null = null;
  let previewUrl: string | null = null;

  // モーダルが開かれたときの処理
  $: if (modalActive) {
    resetModal();
    loadUserData();
  }

  async function loadUserData() {
    if (!roomId || !authUser) return;

    try {
      isLoading = true;
      error = '';

      // RoomUser 情報を取得
      const response = await api.get(`/RoomUsers/room/${roomId}/me`);
      if (response.success) {
        roomUser = response.data;
        // ユーザー名を同期
        applicationUser.displayName = roomUser.displayName;
      }

      // ApplicationUser 情報を取得
      const userResponse = await api.get('/User/me');
      if (userResponse.success) {
        applicationUser = userResponse.data;
      }
    } catch (err) {
      error = 'データの読み込みに失敗しました';
      console.error(err);
    } finally {
      isLoading = false;
    }
  }

  async function saveRoomUserSettings() {
    if (!roomId) return;

    try {
      isLoading = true;
      error = '';
      success = '';

      const response = await api.put(`/RoomUsers/room/${roomId}/me`, roomUser);

      if (response.success) {
        success = '部屋のユーザー設定を保存しました';
        // ストアを更新
        currentRoom.update(room => {
          if (room && room.roomUsers) {
            const index = room.roomUsers.findIndex(u => u.id === roomUser.id);
            if (index !== -1) {
              room.roomUsers[index] = response.data;
            }
          }
          return room;
        });
      }
    } catch (err) {
      error = '保存に失敗しました';
      console.error(err);
    } finally {
      isLoading = false;
    }
  }

  async function saveApplicationUserSettings() {
    if (!authUser) return;

    try {
      isLoading = true;
      error = '';
      success = '';

      const response = await api.put('/User', applicationUser);

      if (response.success) {
        success = 'ユーザー設定を保存しました';
        // 認証ストアを更新
        user.update(u => ({
          ...u!,
          displayName: applicationUser.displayName
        }));
      }
    } catch (err) {
      error = '保存に失敗しました';
      console.error(err);
    } finally {
      isLoading = false;
    }
  }

  async function handleIconUpload() {
    if (!iconFile || !roomId) return;

    try {
      isLoading = true;
      error = '';

      const formData = new FormData();
      formData.append('icon', iconFile);

      const response = await api.post(`/RoomUsers/room/${roomId}/me/icon`, formData, {
        isFileUpload: true
      });

      if (response.success) {
        // RoomUser のアイコンを更新
        roomUser.iconUrl = response.data.iconUrl;

        // ApplicationUser のアイコンファイル名を更新
        if (response.data.iconFileName) {
          applicationUser.iconFileName = response.data.iconFileName;
        }

        success = 'アイコンを更新しました';
      }
    } catch (err) {
      error = 'アイコンの更新に失敗しました';
      console.error(err);
    } finally {
      isLoading = false;
    }
  }

  function handleFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      iconFile = input.files[0];
      // プレビューURLを生成
      previewUrl = URL.createObjectURL(iconFile);
    }
  }

  function resetModal() {
    error = '';
    success = '';
    iconFile = null;
    previewUrl = null;
    activeTab = 'room';
  }

  function closeModal() {
    modals.close('user-setting');
    resetModal();
  }

  // タブ切り替え
  function switchTab(tab: string) {
    activeTab = tab;
  }

  // 現在のタブのコンポーネント
  const currentTabComponent = activeTab === 'room' ?
    ({
      title: '部屋のユーザー設定',
      description: 'この部屋での表示名とアイコンを設定します'
    }) :
    ({
      title: '全般ユーザー設定',
      description: 'アカウント全般の設定を変更します'
    });
</script>

{#if modalActive}
  <Modal
    title={currentTabComponent.title}
    description={currentTabComponent.description}
    on:close={closeModal}
    width="large"
  >
    <div class="user-setting-modal">
      <!-- タブナビゲーション -->
      <div class="tab-navigation">
        <button
          class:tab-active={activeTab === 'room'}
          on:click={() => switchTab('room')}
        >
          部屋のユーザー設定
        </button>
        <button
          class:tab-active={activeTab === 'application'}
          on:click={() => switchTab('application')}
        >
          全般ユーザー設定
        </button>
      </div>

      <!-- タブコンテンツ -->
      <div class="tab-content">
        {#if activeTab === 'room'}
          <!-- RoomUser 設定タブ -->
          <div class="room-user-tab">
            <div class="form-section">
              <h3>表示名</h3>
              <Input
                bind:value={roomUser.displayName}
                placeholder="表示名を入力"
                required
                {minLength}=2
                {maxLength}=50
              />
              <p class="help-text">この部屋での表示名です</p>
            </div>

            <div class="form-section">
              <h3>アイコン</h3>
              <div class="icon-upload">
                <div class="icon-preview">
                  {#if previewUrl}
                    <img src={previewUrl} alt="プレビュー" />
                  {:else if roomUser.iconUrl}
                    <img src={roomUser.iconUrl} alt="現在のアイコン" />
                  {:else}
                    <div class="no-icon">アイコンなし</div>
                  {/if}
                </div>
                <div class="icon-upload-controls">
                  <input
                    type="file"
                    accept="image/*"
                    on:change={handleFileChange}
                    disabled={isLoading}
                  />
                  {#if iconFile}
                    <Button
                      variant="primary"
                      on:click={handleIconUpload}
                      disabled={isLoading}
                    >
                      アイコンを更新
                    </Button>
                  {/if}
                </div>
              </div>
            </div>

            <div class="form-section">
              <h3>設定オプション</h3>
              <div class="options-list">
                <label class="checkbox-label">
                  <input
                    type="checkbox"
                    bind:checked={roomUser.useMainName}
                  />
                  メインの表示名を使用
                </label>
                <label class="checkbox-label">
                  <input
                    type="checkbox"
                    bind:checked={roomUser.useMainIcon}
                  />
                  メインのアイコンを使用
                </label>
              </div>
            </div>
          </div>

        {:else if activeTab === 'application'}
          <!-- ApplicationUser 設定タブ -->
          <div class="application-user-tab">
            <div class="form-section">
              <h3>表示名</h3>
              <Input
                bind:value={applicationUser.displayName}
                placeholder="表示名を入力"
                required
                {minLength}=2
                {maxLength}=50
              />
              <p class="help-text">アプリ全体での表示名です</p>
            </div>

            <div class="form-section">
              <h3>メールアドレス</h3>
              <Input
                bind:value={applicationUser.email}
                type="email"
                placeholder="メールアドレス"
                disabled
              />
              <p class="help-text">メールアドレスは変更できません</p>
            </div>

            <div class="form-section">
              <h3>アカウント設定</h3>
              <div class="info-list">
                <div class="info-item">
                  <span class="info-label">ユーザー名:</span>
                  <span class="info-value">{applicationUser.userName}</span>
                </div>
                <div class="info-item">
                  <span class="info-label">メール確認:</span>
                  <span class="info-value">{applicationUser.emailConfirmed ? '完了' : '未完了'}</span>
                </div>
              </div>
            </div>
          </div>
        {/if}
      </div>

      <!-- メッセージ表示 -->
      {#if error}
        <div class="error-message">
          {error}
        </div>
      {/if}

      {#if success}
        <div class="success-message">
          {success}
        </div>
      {/if}

      <!-- ボタン -->
      <div class="modal-actions">
        {#if activeTab === 'room'}
          <Button
            variant="primary"
            on:click={saveRoomUserSettings}
            disabled={isLoading}
          >
            保存
          </Button>
        {:else if activeTab === 'application'}
          <Button
            variant="primary"
            on:click={saveApplicationUserSettings}
            disabled={isLoading}
          >
            保存
          </Button>
        {/if}
        <Button on:click={closeModal} disabled={isLoading}>
          キャンセル
        </Button>
      </div>
    </div>
  </Modal>
{/if}

<style>
  .user-setting-modal {
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
  }

  .tab-navigation {
    display: flex;
    gap: 0.5rem;
    border-bottom: 1px solid #e5e7eb;
    margin-bottom: 1rem;
  }

  .tab-navigation button {
    padding: 0.5rem 1rem;
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    color: #6b7280;
    font-weight: 500;
    transition: all 0.2s;
  }

  .tab-navigation button:hover {
    color: #374151;
  }

  .tab-navigation button.tab-active {
    color: #3b82f6;
    border-bottom-color: #3b82f6;
  }

  .tab-content {
    min-height: 300px;
  }

  .form-section {
    margin-bottom: 1.5rem;
  }

  .form-section h3 {
    font-size: 1rem;
    font-weight: 600;
    margin-bottom: 0.5rem;
    color: #374151;
  }

  .help-text {
    font-size: 0.875rem;
    color: #6b7280;
    margin-top: 0.25rem;
  }

  .icon-upload {
    display: flex;
    gap: 1rem;
    align-items: flex-start;
  }

  .icon-preview {
    width: 80px;
    height: 80px;
    border: 2px dashed #d1d5db;
    border-radius: 0.5rem;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
  }

  .icon-preview img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .no-icon {
    color: #9ca3af;
    font-size: 0.875rem;
  }

  .icon-upload-controls {
    flex: 1;
  }

  .icon-upload-controls input[type="file"] {
    width: 100%;
    padding: 0.5rem;
    border: 1px solid #d1d5db;
    border-radius: 0.375rem;
    margin-bottom: 0.5rem;
  }

  .options-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .checkbox-label {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .checkbox-label input[type="checkbox"] {
    width: 1rem;
    height: 1rem;
  }

  .info-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .info-item {
    display: flex;
    justify-content: space-between;
    padding: 0.5rem 0;
    border-bottom: 1px solid #f3f4f6;
  }

  .info-label {
    font-weight: 500;
    color: #374151;
  }

  .info-value {
    color: #6b7280;
  }

  .error-message {
    color: #dc2626;
    background-color: #fef2f2;
    padding: 0.75rem;
    border-radius: 0.375rem;
    font-size: 0.875rem;
  }

  .success-message {
    color: #059669;
    background-color: #f0fdf4;
    padding: 0.75rem;
    border-radius: 0.375rem;
    font-size: 0.875rem;
  }

  .modal-actions {
    display: flex;
    gap: 0.75rem;
    justify-content: flex-end;
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid #e5e7eb;
  }
</style>
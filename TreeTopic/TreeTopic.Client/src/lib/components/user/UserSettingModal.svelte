<script lang="ts">
  import { api } from '$lib/api/client';
  import { ui, activeModals } from '$lib/stores/ui';
  import { auth } from '$lib/stores/auth';
  import { currentRoom, type CurrentRoomUser } from '$lib/stores/rooms';
  import type { User } from '$lib/stores/auth';

  import Button from '$lib/components/common/Button.svelte';
  import Input from '$lib/components/common/Input.svelte';
  import Modal from '$lib/components/common/Modal.svelte';
  import PushNotificationSettings from '$lib/components/push/PushNotificationSettings.svelte';

  // APIレスポンスの型
  interface ApiResponse<T> {
    success: boolean;
    data: T;
  }

  // Props
  interface Props {
    roomId: string;
    onclose?: () => void;
  }
  let { roomId, onclose }: Props = $props();

  // Modal ID
  const modalId = 'user-setting';
  let isOpen = $derived.by(() => $activeModals.some((m) => m.id === modalId));

  // ストアから値を取得（リアクティブ）
  let authUser = $derived($auth.user);

  // UI 状態
  let isLoading = $state(false);
  let error = $state('');
  let success = $state('');

  // タブの状態
  let activeTab = $state('room'); // 'room' または 'application'

  // RoomUser 設定状態（CurrentRoomUserをベースに拡張）
  let roomUser = $state<CurrentRoomUser & { useMainName: boolean }>({
    id: '',
    displayName: '',
    iconUrl: '',
    useMainIcon: false,
    useMainName: false
  });

  // ApplicationUser 設定状態（Userをベースに拡張）
  let applicationUser = $state<User & {
    normalizedUserName?: string;
    normalizedEmail?: string;
    emailConfirmed?: boolean;
    passwordHash?: string;
    securityStamp?: string;
    concurrencyStamp?: string;
    phoneNumber?: string;
    phoneNumberConfirmed?: boolean;
    twoFactorEnabled?: boolean;
    lockoutEnd?: Date | null;
    lockoutEnabled?: boolean;
    accessFailedCount?: number;
    iconFileName?: string;
    sub?: string;
  }>({
    id: '',
    userName: '',
    email: '',
    displayName: '',
    iconUrl: '',
    roles: []
  });

  // ファイルアップロード用
  let iconFile = $state<File | null>(null);
  let previewUrl = $state<string | null>(null);

  // ApplicationUser用のファイルアップロード
  let applicationIconFile = $state<File | null>(null);
  let applicationPreviewUrl = $state<string | null>(null);

  // ファイル入力の参照（DOM参照のため $state は不要）
  // svelte-ignore <non_reactive_update>
  let roomFileInputNode: HTMLInputElement;
  // svelte-ignore <non_reactive_update>
  let applicationFileInputNode: HTMLInputElement;

  // 元の設定を保存（復元用）
  let originalRoomUser = $state<(CurrentRoomUser & { useMainName: boolean }) | null>(null);
  let originalApplicationUser = $state<(User & {
    normalizedUserName?: string;
    normalizedEmail?: string;
    emailConfirmed?: boolean;
    passwordHash?: string;
    securityStamp?: string;
    concurrencyStamp?: string;
    phoneNumber?: string;
    phoneNumberConfirmed?: boolean;
    twoFactorEnabled?: boolean;
    lockoutEnd?: Date | null;
    lockoutEnabled?: boolean;
    accessFailedCount?: number;
    iconFileName?: string;
    sub?: string;
  }) | null>(null);

  // 設定が変更されたかどうかを検出
  let hasChangesRoom = $derived.by(() => {
    return originalRoomUser && JSON.stringify(roomUser) !== JSON.stringify(originalRoomUser);
  });
  let hasChangesApplication = $derived.by(() => {
    return originalApplicationUser && JSON.stringify(applicationUser) !== JSON.stringify(originalApplicationUser);
  });

  // モーダルが開かれたときの処理
  $effect(() => {
    if (isOpen) {
      // エラーと成功メッセージをクリア
      error = '';
      success = '';
      iconFile = null;
      previewUrl = null;
      applicationIconFile = null;
      applicationPreviewUrl = null;
      activeTab = 'room';
      // データを読み込んで元の設定を保存
      loadUserData();
    }
  });

  async function loadUserData() {
    if (!roomId || !authUser) return;

    try {
      isLoading = true;
      error = '';

      // テナントを取得
      const tenant = api.getCurrentTenant();

      // RoomUser 情報を取得。未作成ならここで作成して設定画面を継続利用できるようにする。
      let roomUserData: typeof roomUser;
      try {
        roomUserData = await api.get(`/${tenant}/api/roomusers/room/${roomId}/me`) as typeof roomUser;
      } catch (err) {
        if (err instanceof api.ApiError && err.status === 404) {
          roomUserData = await api.post(`/${tenant}/api/roomusers/room/${roomId}/join`, {
            useMainName: true,
            useMainIcon: true
          }) as typeof roomUser;
        } else {
          throw err;
        }
      }

      roomUser = roomUserData;
      // ユーザー名を同期
      applicationUser.displayName = roomUser.displayName;
      // 元の設定がまだ保存されていない場合のみ保存
      if (!originalRoomUser) {
        originalRoomUser = JSON.parse(JSON.stringify(roomUserData));
      }

      // ApplicationUser 情報を取得（直接データを取得）
      const userData = await api.get(`/${tenant}/api/users/me`) as typeof applicationUser;
      applicationUser = userData;
      // 元の設定がまだ保存されていない場合のみ保存
      if (!originalApplicationUser) {
        originalApplicationUser = JSON.parse(JSON.stringify(userData));
      }
    } catch (err) {
      error = 'Failed to load data';
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

      // テナントを取得
      const tenant = api.getCurrentTenant();

      const updatedRoomUser = await api.put(`/${tenant}/api/roomusers/room/${roomId}/me`, roomUser) as typeof roomUser;

      success = 'Room user settings saved';
      // 元の設定を更新
      originalRoomUser = JSON.parse(JSON.stringify(updatedRoomUser));
    } catch (err) {
      error = 'Failed to save';
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

      // テナントを取得
      const tenant = api.getCurrentTenant();

      const updatedUser = await api.put(`/${tenant}/api/users/me`, applicationUser) as typeof applicationUser;

      success = 'User settings saved';
      // 認証ストアを更新
      if (updatedUser) {
        auth.updateUser({
          displayName: updatedUser.displayName ?? updatedUser.displayName,
          iconUrl: updatedUser.iconUrl ?? updatedUser.iconUrl
        });
      }
      // 元の設定を更新
      originalApplicationUser = JSON.parse(JSON.stringify(updatedUser));
    } catch (err) {
      error = 'Failed to save';
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

      // テナントを取得
      const tenant = api.getCurrentTenant();

      const formData = new FormData();
      formData.append('icon', iconFile);

      const response = await api.post(`/${tenant}/api/roomusers/room/${roomId}/me/icon`, formData) as { iconUrl: string; iconFileName?: string };

      // RoomUser のアイコンを更新
      roomUser.iconUrl = response.iconUrl;

      // ApplicationUser のアイコンファイル名を更新
      if (response.iconFileName) {
        applicationUser.iconFileName = response.iconFileName;
      }

      success = 'Icon updated successfully';
    } catch (err) {
      error = 'Failed to update icon';
      console.error(err);
    } finally {
      isLoading = false;
    }
  }

  async function handleApplicationIconUpload() {
    if (!applicationIconFile || !authUser) return;

    try {
      isLoading = true;
      error = '';

      // テナントを取得
      const tenant = api.getCurrentTenant();

      const formData = new FormData();
      formData.append('icon', applicationIconFile);

      const response = await api.post(`/${tenant}/api/users/me/icon`, formData) as { iconUrl: string; iconFileName?: string };

      // ApplicationUser のアイコンを更新
      if (response.iconUrl) {
        applicationUser.iconUrl = response.iconUrl;
      }
      if (response.iconFileName) {
        applicationUser.iconFileName = response.iconFileName;
      }

      success = 'Icon updated successfully';
    } catch (err) {
      error = 'Failed to update icon';
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

  function handleApplicationFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      applicationIconFile = input.files[0];
      // プレビューURLを生成
      applicationPreviewUrl = URL.createObjectURL(applicationIconFile);
    }
  }

  function resetModal() {
    error = '';
    success = '';
    iconFile = null;
    previewUrl = null;
    applicationIconFile = null;
    applicationPreviewUrl = null;
    activeTab = 'room';
  }

  // 設定を復元する
  function restoreSettings() {
    if (activeTab === 'room' && originalRoomUser) {
      roomUser = JSON.parse(JSON.stringify(originalRoomUser));
      // プレビューをクリア
      iconFile = null;
      previewUrl = null;
      // ファイル入力をリセット
      if (roomFileInputNode) {
        roomFileInputNode.value = '';
      }
      success = 'Original settings restored';
      // メッセージを3秒後に消す
      setTimeout(() => { success = ''; }, 3000);
    } else if (activeTab === 'application' && originalApplicationUser) {
      applicationUser = JSON.parse(JSON.stringify(originalApplicationUser));
      // プレビューをクリア
      applicationIconFile = null;
      applicationPreviewUrl = null;
      // ファイル入力をリセット
      if (applicationFileInputNode) {
        applicationFileInputNode.value = '';
      }
      success = 'Original settings restored';
      // メッセージを3秒後に消す
      setTimeout(() => { success = ''; }, 3000);
    }
  }

  function closeModal() {
    ui.closeModal(modalId);
    resetModal();
  }

  // タブ切り替え
  function switchTab(tab: string) {
    activeTab = tab;
  }

  // 現在のタブのコンポーネント
  let currentTabComponent = $derived(activeTab === 'room' ?
    {
      title: 'Room User Settings',
      description: 'Set display name and icon for this room'
    } :
    {
      title: 'General User Settings',
      description: 'Change account-wide settings'
    });
</script>

{#if isOpen}
  <Modal
    {isOpen}
    title={currentTabComponent.title}
    onClose={closeModal}
    size="large"
  >
    <div class="user-setting-modal">
      <!-- タブナビゲーション -->
      <div class="tab-navigation">
        <button
          class:tab-active={activeTab === 'room'}
          onclick={() => switchTab('room')}
        >
          Room User Settings
        </button>
        <button
          class:tab-active={activeTab === 'application'}
          onclick={() => switchTab('application')}
        >
          General User Settings
        </button>
      </div>

      <!-- タブコンテンツ -->
      <div class="tab-content">
        {#if activeTab === 'room'}
          <!-- RoomUser 設定タブ -->
          <div class="room-user-tab">
            <div class="form-section">
              <h3>Display Name</h3>
              <Input
                bind:value={roomUser.displayName}
                placeholder="Enter display name"
                required
                disabled={roomUser.useMainName}
              />
              <p class="help-text">This is your display name in this room</p>
            </div>

            <div class="form-section">
              <h3>Icon</h3>
              <div class="icon-upload">
                <div class="icon-preview">
                  {#if previewUrl}
                    <img src={previewUrl} alt="Preview" />
                  {:else if roomUser.iconUrl}
                    <img src={roomUser.iconUrl} alt="Current icon" />
                  {:else}
                    <div class="no-icon">No icon</div>
                  {/if}
                </div>
                <div class="icon-upload-controls">
                  <input
                    type="file"
                    accept="image/*"
                    onchange={handleFileChange}
                    disabled={isLoading || roomUser.useMainIcon}
                    bind:this={roomFileInputNode}
                  />
                  {#if iconFile}
                    <Button
                      variant="primary"
                      onclick={handleIconUpload}
                      disabled={isLoading || roomUser.useMainIcon}
                    >
                      Update Icon
                    </Button>
                  {/if}
                </div>
              </div>
            </div>

            <div class="form-section">
              <h3>Settings Options</h3>
              <div class="options-list">
                <label class="checkbox-label">
                  <input
                    type="checkbox"
                    bind:checked={roomUser.useMainName}
                  />
                  Use main display name
                </label>
                <label class="checkbox-label">
                  <input
                    type="checkbox"
                    bind:checked={roomUser.useMainIcon}
                  />
                  Use main icon
                </label>
              </div>
            </div>
          </div>

        {:else if activeTab === 'application'}
          <!-- ApplicationUser 設定タブ -->
          <div class="application-user-tab">
            <div class="form-section">
              <h3>Display Name</h3>
              <Input
                bind:value={applicationUser.displayName}
                placeholder="Enter display name"
                required
              />
              <p class="help-text">This is your display name across the entire application</p>
            </div>

            <div class="form-section">
              <h3>Icon</h3>
              <div class="icon-upload">
                <div class="icon-preview">
                  {#if applicationPreviewUrl}
                    <img src={applicationPreviewUrl} alt="Preview" />
                  {:else if applicationUser.iconUrl}
                    <img src={applicationUser.iconUrl} alt="Current icon" />
                  {:else}
                    <div class="no-icon">No icon</div>
                  {/if}
                </div>
                <div class="icon-upload-controls">
                  <input
                    type="file"
                    accept="image/*"
                    onchange={handleApplicationFileChange}
                    disabled={isLoading}
                    bind:this={applicationFileInputNode}
                  />
                  {#if applicationIconFile}
                    <Button
                      variant="primary"
                      onclick={handleApplicationIconUpload}
                      disabled={isLoading}
                    >
                      Update Icon
                    </Button>
                  {/if}
                </div>
              </div>
              <p class="help-text">This is your icon across the entire application</p>
            </div>

            <div class="form-section">
              <PushNotificationSettings />
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
          <!-- 復元ボタン（設定変更がある場合のみ表示） -->
          {#if hasChangesRoom}
            <Button
              variant="secondary"
              onclick={restoreSettings}
              disabled={isLoading}
            >
              Restore Original Settings
            </Button>
          {/if}
          <Button
            variant="primary"
            onclick={saveRoomUserSettings}
            disabled={isLoading}
          >
            Save
          </Button>
        {:else if activeTab === 'application'}
          <!-- 復元ボタン（設定変更がある場合のみ表示） -->
          {#if hasChangesApplication}
            <Button
              variant="secondary"
              onclick={restoreSettings}
              disabled={isLoading}
            >
              Restore Original Settings
            </Button>
          {/if}
          <Button
            variant="primary"
            onclick={saveApplicationUserSettings}
            disabled={isLoading}
          >
            Save
          </Button>
        {/if}
        <Button onclick={closeModal} disabled={isLoading}>
          Cancel
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

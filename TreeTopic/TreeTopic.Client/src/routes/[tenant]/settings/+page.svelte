<script lang="ts">
  import { isAuthenticated, currentUser } from '$lib/stores/auth';
  import AppLayout from '$lib/components/layout/AppLayout.svelte';
  import RoomSelector from '$lib/components/rooms/RoomSelector.svelte';
  import Button from '$lib/components/common/Button.svelte';

  let activeTab = $state('general');

  const tabs = [
    { id: 'general', label: 'General Settings' },
    { id: 'profile', label: 'Profile' },
    { id: 'notifications', label: 'Notifications' },
    { id: 'privacy', label: 'Privacy & Security' },
  ];
</script>

<svelte:head>
  <title>Settings - TreeTopic</title>
</svelte:head>

{#if $isAuthenticated}
  <AppLayout>
    {#snippet headerContent()}
      <div class="flex items-center justify-between w-full">
        <RoomSelector navigateOnSelect={false} />
        <h1 class="text-xl font-bold text-text">Settings</h1>
        <div></div>
      </div>
    {/snippet}

    {#snippet sidebarContent()}
      <div class="space-y-2 p-5">
        {#each tabs as tab}
          <button
            onclick={() => (activeTab = tab.id)}
            class="w-full flex items-center gap-3 px-5 py-3 rounded-lg transition-colors {activeTab ===
            tab.id
              ? 'bg-primary text-white'
              : 'text-text hover:bg-surface'}"
          >
            <span class="font-semibold">{tab.label}</span>
          </button>
        {/each}
      </div>
    {/snippet}

    {#snippet mainContent()}
      <div class="flex-1 overflow-y-auto p-8 bg-white">
        <div class="max-w-2xl">
            {#if activeTab === 'general'}
              <div class="space-y-6">
                <div>
                  <h2 class="text-2xl font-bold text-text mb-4">General Settings</h2>
                </div>

                <div class="border-b border-border pb-6">
                  <h3 class="text-lg font-semibold text-text mb-4">Theme</h3>
                  <div class="space-y-3">
                    <label class="flex items-center gap-3 cursor-pointer">
                      <input type="radio" name="theme" value="light" class="w-4 h-4" />
                      <span class="text-text">Light Mode</span>
                    </label>
                    <label class="flex items-center gap-3 cursor-pointer">
                      <input type="radio" name="theme" value="dark" class="w-4 h-4" />
                      <span class="text-text">Dark Mode (Coming Soon)</span>
                    </label>
                  </div>
                </div>

                <div class="border-b border-border pb-6">
                  <h3 class="text-lg font-semibold text-text mb-4">Display</h3>
                  <div class="space-y-3">
                    <label class="flex items-center gap-3 cursor-pointer">
                      <input type="checkbox" class="w-4 h-4 accent-primary" />
                      <span class="text-text">Compact Layout</span>
                    </label>
                    <label class="flex items-center gap-3 cursor-pointer">
                      <input type="checkbox" class="w-4 h-4 accent-primary" checked />
                      <span class="text-text">Show Message Timestamps</span>
                    </label>
                  </div>
                </div>
              </div>
            {:else if activeTab === 'profile'}
              <div class="space-y-6">
                <div>
                  <h2 class="text-2xl font-bold text-text mb-4">Profile Settings</h2>
                </div>

                <div class="space-y-4">
                  <div class="flex items-center gap-3">
                    {#if $currentUser?.avatar}
                      <img
                        src={$currentUser.avatar}
                        alt={$currentUser?.displayName || 'User'}
                        class="w-8 h-8 rounded-full flex-shrink-0"
                      />
                    {:else}
                      <div class="w-8 h-8 rounded-full bg-primary text-white flex items-center justify-center text-xs font-bold flex-shrink-0">
                        {$currentUser?.displayName?.charAt(0) ?? 'U'}
                      </div>
                    {/if}
                    <div>
                      <p class="text-sm text-text-light">Profile name</p>
                      <p class="text-base font-semibold text-text">{$currentUser?.displayName || ''}</p>
                    </div>
                  </div>

                  <div>
                    <label for="profile-display-name" class="block text-sm font-semibold text-text mb-2">Display Name</label>
                    <input
                      type="text"
                      id="profile-display-name"
                      value={$currentUser?.displayName || ''}
                      placeholder="Your display name"
                      class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:border-primary"
                    />
                  </div>

                  <div>
                    <label for="profile-email" class="block text-sm font-semibold text-text mb-2">Email</label>
                    <input
                      type="email"
                      id="profile-email"
                      value={$currentUser?.email || ''}
                      placeholder="Your email"
                      disabled
                      class="w-full px-4 py-2 border border-border rounded-lg bg-surface disabled:opacity-60"
                    />
                  </div>

                  <div>
                    <label for="profile-avatar-url" class="block text-sm font-semibold text-text mb-2">Avatar URL</label>
                    <input
                      type="url"
                      id="profile-avatar-url"
                      placeholder="https://example.com/avatar.jpg"
                      class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:border-primary"
                    />
                  </div>

                  <div class="pt-4">
                    <Button variant="primary">Save Profile</Button>
                  </div>
                </div>
              </div>
            {:else if activeTab === 'notifications'}
              <div class="space-y-6">
                <div>
                  <h2 class="text-2xl font-bold text-text mb-4">Notification Settings</h2>
                </div>

                <div class="space-y-4">
                  <div class="flex items-center justify-between p-4 bg-surface rounded-lg border border-border">
                    <div>
                      <p class="font-semibold text-text">Message Notifications</p>
                      <p class="text-sm text-text-light">Get notified when new messages arrive</p>
                    </div>
                    <input type="checkbox" class="w-4 h-4 accent-primary" checked />
                  </div>

                  <div class="flex items-center justify-between p-4 bg-surface rounded-lg border border-border">
                    <div>
                      <p class="font-semibold text-text">Room Invitations</p>
                      <p class="text-sm text-text-light">Get notified when invited to rooms</p>
                    </div>
                    <input type="checkbox" class="w-4 h-4 accent-primary" checked />
                  </div>

                  <div class="flex items-center justify-between p-4 bg-surface rounded-lg border border-border">
                    <div>
                      <p class="font-semibold text-text">Brainstorm Updates</p>
                      <p class="text-sm text-text-light">Get notified of new ideas in brainstorms</p>
                    </div>
                    <input type="checkbox" class="w-4 h-4 accent-primary" checked />
                  </div>
                </div>
              </div>
            {:else if activeTab === 'privacy'}
              <div class="space-y-6">
                <div>
                  <h2 class="text-2xl font-bold text-text mb-4">Privacy & Security</h2>
                </div>

                <div class="border-b border-border pb-6">
                  <h3 class="text-lg font-semibold text-text mb-4">Change Password</h3>
                  <div class="space-y-4">
                    <input
                      type="password"
                      placeholder="Current password"
                      class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:border-primary"
                    />
                    <input
                      type="password"
                      placeholder="New password"
                      class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:border-primary"
                    />
                    <input
                      type="password"
                      placeholder="Confirm new password"
                      class="w-full px-4 py-2 border border-border rounded-lg focus:outline-none focus:border-primary"
                    />
                    <Button variant="primary">Update Password</Button>
                  </div>
                </div>

                <div class="border-b border-border pb-6">
                  <h3 class="text-lg font-semibold text-text mb-4">Active Sessions</h3>
                  <div class="space-y-2 text-sm text-text-light">
                    <p>Current session • Last active: Just now</p>
                    <Button variant="secondary" size="small">Sign Out</Button>
                  </div>
                </div>

                <div>
                  <h3 class="text-lg font-semibold text-text mb-4">Danger Zone</h3>
                  <Button variant="danger">Delete Account</Button>
                </div>
              </div>
            {/if}
          </div>
        </div>
      {/snippet}
  </AppLayout>
{/if}

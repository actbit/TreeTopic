<script lang="ts">
  import { onMount } from 'svelte';

  interface DropdownItem {
    label: string;
    value: string | number;
    icon?: string;
    disabled?: boolean;
  }

  interface Props {
    items: DropdownItem[];
    value?: string | number;
    placeholder?: string;
    label?: string;
    error?: string;
    disabled?: boolean;
    onChange?: (value: string | number) => void;
  }

  let {
    items,
    value = $bindable<string | number>(''),
    placeholder = 'Select an option',
    label,
    error,
    disabled = false,
    onChange,
  }: Props = $props();

  let isOpen = $state(false);
  let dropdownElement: HTMLDivElement | undefined = $state();

  const selectedLabel = $derived(
    items.find((item) => item.value === value)?.label || placeholder
  );

  function toggleDropdown() {
    if (!disabled) {
      isOpen = !isOpen;
    }
  }

  function selectItem(item: DropdownItem) {
    if (!item.disabled) {
      value = item.value;
      if (onChange) {
        onChange(item.value);
      }
      isOpen = false;
    }
  }

  function handleClickOutside(event: MouseEvent) {
    if (dropdownElement && !dropdownElement.contains(event.target as Node)) {
      isOpen = false;
    }
  }

  onMount(() => {
    document.addEventListener('click', handleClickOutside);

    return () => {
      document.removeEventListener('click', handleClickOutside);
    };
  });
</script>

<div class="dropdown-group">
  {#if label}
    <label class="dropdown-label">
      {label}
    </label>
  {/if}

  <div class="dropdown-wrapper" bind:this={dropdownElement}>
    <button
      type="button"
      on:click={toggleDropdown}
      {disabled}
      class="dropdown-button {error ? 'dropdown-button-error' : ''}"
    >
      <span class="dropdown-selected-label">{selectedLabel}</span>
      <span class="dropdown-arrow {isOpen ? 'dropdown-arrow-open' : ''}">▼</span>
    </button>

    {#if isOpen}
      <div class="dropdown-menu">
        {#each items as item (item.value)}
          <button
            type="button"
            on:click={() => selectItem(item)}
            disabled={item.disabled}
            class="dropdown-menu-item {value === item.value ? 'dropdown-menu-item-selected' : ''}"
          >
            {#if item.icon}
              <span class="dropdown-menu-item-icon">{item.icon}</span>
            {/if}
            {item.label}
          </button>
        {/each}
      </div>
    {/if}
  </div>

  {#if error}
    <p class="dropdown-error">{error}</p>
  {/if}
</div>

<style>
  .dropdown-group {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .dropdown-label {
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
  }

  .dropdown-wrapper {
    position: relative;
  }

  .dropdown-button {
    width: 100%;
    padding: 8px 16px;
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    text-align: left;
    font-size: var(--font-size-base);
    background-color: var(--color-background);
    transition: all 0.2s ease;
    display: flex;
    align-items: center;
    justify-content: space-between;
    cursor: pointer;
  }

  .dropdown-button:hover:not(:disabled) {
    border-color: var(--color-primary);
  }

  .dropdown-button:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .dropdown-button-error {
    border-color: var(--color-error);
  }

  .dropdown-selected-label {
    color: var(--color-text-light);
  }

  .dropdown-arrow {
    transition: transform 0.2s ease;
  }

  .dropdown-arrow-open {
    transform: rotate(180deg);
  }

  .dropdown-menu {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    margin-top: 4px;
    background-color: var(--color-background);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    box-shadow: var(--shadow-lg);
    z-index: 50;
    max-height: 256px;
    overflow-y: auto;
    animation: slideInDown 0.2s ease-out;
  }

  .dropdown-menu-item {
    width: 100%;
    padding: 8px 16px;
    text-align: left;
    font-size: var(--font-size-base);
    color: var(--color-text);
    background-color: transparent;
    border: none;
    cursor: pointer;
    transition: background-color 0.2s ease;
  }

  .dropdown-menu-item:hover:not(:disabled) {
    background-color: var(--color-surface);
  }

  .dropdown-menu-item:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .dropdown-menu-item-selected {
    background-color: var(--color-surface);
    border-left: 2px solid var(--color-primary);
  }

  .dropdown-menu-item-icon {
    margin-right: 8px;
  }

  .dropdown-error {
    font-size: var(--font-size-xs);
    color: var(--color-error);
  }

  @keyframes slideInDown {
    from {
      opacity: 0;
      transform: translateY(-10px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }
</style>

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

<div class="flex flex-col gap-1">
  {#if label}
    <label class="text-sm font-semibold text-text">
      {label}
    </label>
  {/if}

  <div class="relative" bind:this={dropdownElement}>
    <button
      type="button"
      on:click={toggleDropdown}
      {disabled}
      class="w-full px-4 py-2 border border-border rounded-sm text-left text-base bg-white transition-all duration-200
        hover:border-primary disabled:opacity-60 disabled:cursor-not-allowed
        flex items-center justify-between
        {error ? 'border-error' : ''}"
    >
      <span class="text-text-light">{selectedLabel}</span>
      <span class="transition-transform {isOpen ? 'rotate-180' : ''}">▼</span>
    </button>

    {#if isOpen}
      <div
        class="absolute top-full left-0 right-0 mt-1 bg-white border border-border rounded-sm shadow-lg z-50 max-h-64 overflow-y-auto slide-in-down"
      >
        {#each items as item (item.value)}
          <button
            type="button"
            on:click={() => selectItem(item)}
            disabled={item.disabled}
            class="w-full px-4 py-2 text-left text-base text-text hover:bg-surface transition-colors
              disabled:opacity-50 disabled:cursor-not-allowed
              {value === item.value ? 'bg-surface border-l-2 border-primary' : ''}"
          >
            {#if item.icon}
              <span class="mr-2">{item.icon}</span>
            {/if}
            {item.label}
          </button>
        {/each}
      </div>
    {/if}
  </div>

  {#if error}
    <p class="text-xs text-error">{error}</p>
  {/if}
</div>

<style>
  :global(.slide-in-down) {
    animation: slideInDown 0.2s ease-out;
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

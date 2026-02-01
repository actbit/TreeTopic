<script lang="ts">
  import { page } from '$app/stores';

  interface BreadcrumbItem {
    label: string;
    href?: string;
  }

  interface Props {
    items: BreadcrumbItem[];
  }

  let { items }: Props = $props();
</script>

<nav class="flex items-center gap-2 text-sm">
  {#each items as item, index}
    {#if index > 0}
      <span class="text-text-light">/</span>
    {/if}
    {#if item.href}
      <a
        href={item.href}
        class="text-text-light hover:text-primary transition-colors"
      >
        {item.label}
      </a>
    {:else}
      <span class="text-text font-medium">{item.label}</span>
    {/if}
  {/each}
</nav>

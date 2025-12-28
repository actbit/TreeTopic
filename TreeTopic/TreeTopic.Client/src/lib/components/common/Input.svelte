<script lang="ts">
  type InputType = 'text' | 'email' | 'password' | 'number' | 'search' | 'url' | 'tel';

  interface Props {
    label?: string;
    value?: string;
    type?: InputType;
    placeholder?: string;
    error?: string;
    disabled?: boolean;
    required?: boolean;
    icon?: string;
    helperText?: string;
    onchange?: (e: Event) => void;
    oninput?: (e: Event) => void;
  }

  let {
    label,
    value = $bindable(''),
    type = 'text',
    placeholder,
    error,
    disabled = false,
    required = false,
    icon,
    helperText,
    onchange,
    oninput,
  }: Props = $props();
</script>

<div class="flex flex-col gap-1">
  {#if label}
    <label class="text-sm font-semibold text-text">
      {label}
      {#if required}
        <span class="text-error">*</span>
      {/if}
    </label>
  {/if}

  <div class="relative flex items-center">
    {#if icon}
      <span class="absolute left-3 text-text-light text-lg">{icon}</span>
    {/if}

    <input
      {type}
      bind:value
      {placeholder}
      {disabled}
      {onchange}
      {oninput}
      class="w-full px-4 py-2 border border-border rounded-sm text-base transition-all duration-200
        placeholder:text-text-light
        focus:outline-none focus:border-primary focus:shadow-sm
        disabled:bg-surface disabled:cursor-not-allowed disabled:opacity-60
        {icon ? 'pl-10' : ''}
        {error ? 'border-error' : ''}"
    />
  </div>

  {#if error}
    <p class="text-xs text-error">{error}</p>
  {:else if helperText}
    <p class="text-xs text-text-light">{helperText}</p>
  {/if}
</div>

<style>
  div {
    font-family: var(--font-family-base);
  }

  input {
    font-family: var(--font-family-base);
  }

  label {
    color: var(--color-text);
  }
</style>

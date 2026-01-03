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

<div class="input-group">
  {#if label}
    <label class="input-label">
      {label}
      {#if required}
        <span class="input-required">*</span>
      {/if}
    </label>
  {/if}

  <div class="input-wrapper">
    {#if icon}
      <span class="input-icon">{icon}</span>
    {/if}

    <input
      {type}
      bind:value
      {placeholder}
      {disabled}
      {onchange}
      {oninput}
      class="input-field {icon ? 'input-with-icon' : ''} {error ? 'input-error' : ''}"
    />
  </div>

  {#if error}
    <p class="input-message input-message-error">{error}</p>
  {:else if helperText}
    <p class="input-message input-message-helper">{helperText}</p>
  {/if}
</div>

<style>
  .input-group {
    display: flex;
    flex-direction: column;
    gap: 4px;
    font-family: var(--font-family-base);
  }

  .input-label {
    font-size: var(--font-size-sm);
    font-weight: 600;
    color: var(--color-text);
  }

  .input-required {
    color: var(--color-error);
  }

  .input-wrapper {
    position: relative;
    display: flex;
    align-items: center;
  }

  .input-icon {
    position: absolute;
    left: 12px;
    color: var(--color-text-light);
    font-size: var(--font-size-lg);
  }

  .input-field {
    width: 100%;
    padding: 8px 16px;
    font-family: var(--font-family-base);
    font-size: var(--font-size-base);
    border: 1px solid var(--color-border);
    border-radius: var(--border-radius-sm);
    transition: all 0.2s ease;
  }

  .input-field::placeholder {
    color: var(--color-text-light);
  }

  .input-field:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: var(--shadow-sm);
  }

  .input-field:disabled {
    background-color: var(--color-surface);
    cursor: not-allowed;
    opacity: 0.6;
  }

  .input-with-icon {
    padding-left: 40px;
  }

  .input-error {
    border-color: var(--color-error);
  }

  .input-message {
    font-size: var(--font-size-xs);
  }

  .input-message-error {
    color: var(--color-error);
  }

  .input-message-helper {
    color: var(--color-text-light);
  }
</style>

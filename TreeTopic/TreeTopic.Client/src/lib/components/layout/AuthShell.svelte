<script lang="ts">
  export let title = 'TreeTopic';
  export let subtitle: string | null = null;
  export let description: string | null = null;
  export let footerText = 'Secured by OIDC authentication';
  export let showFooter = true;
</script>

<div class="auth-page">
  <section class="auth-card">
    <header class="auth-card__header">
      <h1 class="auth-card__title">{title}</h1>
      {#if subtitle}
        <p class="auth-card__subtitle">{subtitle}</p>
      {/if}
      {#if description}
        <p class="auth-card__subtitle auth-card__description">{description}</p>
      {/if}
    </header>

    <div class="auth-card__body">
      <slot />
    </div>

    {#if showFooter}
      <footer class="auth-card__footer">
        <slot name="footer">{footerText}</slot>
      </footer>
    {/if}
  </section>
</div>

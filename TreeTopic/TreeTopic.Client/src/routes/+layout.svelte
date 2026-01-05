<script lang="ts">
	import '$lib/styles/global.css';
	import { isAuthenticated } from '$lib/stores/auth';
	import { ui } from '$lib/stores/ui';
	import { onMount } from 'svelte';

	onMount(() => {
		// Restore UI state from localStorage
		ui.restoreState();

		// Set initial viewport size
		const updateViewportSize = () => {
			ui.setViewportSize(window.innerWidth, window.innerHeight);
		};

		updateViewportSize();
		window.addEventListener('resize', updateViewportSize);

		return () => {
			window.removeEventListener('resize', updateViewportSize);
		};
	});
</script>

<svelte:head>
	<meta charset="utf-8" />
	<meta name="viewport" content="width=device-width, initial-scale=1" />
	<title>TreeTopic</title>
</svelte:head>

<div class="min-h-screen bg-background font-sans">
	<slot />
</div>

<style>
	:global(html, body) {
		margin: 0;
		padding: 0;
		font-family: var(--font-family-base);
		background-color: var(--color-background);
		color: var(--color-text);
	}

	:global(*) {
		box-sizing: border-box;
	}

	:global(a) {
		color: var(--color-primary);
		text-decoration: none;
	}

	:global(a:hover) {
		text-decoration: underline;
	}
</style>

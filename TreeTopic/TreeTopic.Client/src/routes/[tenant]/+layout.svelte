<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { auth } from '$lib/stores/auth';
	import { onMount } from 'svelte';

	let { children } = $props();

	let isChecking = $state(true);
	let isAuthenticated = $state(false);
	let tenant = $state('');

	// 認証が必要ないパス
	const publicPaths = ['/login', '/auth/login', '/setup', '/auth/check'];

	function isPublicPath(pathname: string): boolean {
		return publicPaths.some(p => pathname.includes(p));
	}

	onMount(async () => {
		// テナントを取得
		tenant = $page.params.tenant || '';

		if (!tenant) {
			// テナントがない場合はホームへ
			goto('/');
			return;
		}

		// パブリックパスの場合は認証チェックをスキップ
		if (isPublicPath($page.url.pathname)) {
			isChecking = false;
			return;
		}

		// バックエンドで認証状態を確認
		const isValid = await auth.checkSession(tenant);

		if (!isValid) {
			// 未認証の場合はログインページへリダイレクト
			const returnUrl = encodeURIComponent($page.url.pathname + $page.url.search + $page.url.hash);
			goto(`/${tenant}/auth/login?returnUrl=${returnUrl}`);
			return;
		}

		isAuthenticated = true;
		isChecking = false;
	});

	// ページ遷移時に認証チェックを実行
	$effect(() => {
		const currentPath = $page.url.pathname;
		let cancelled = false;

		const run = async () => {
			// テナントを更新
			tenant = $page.params.tenant || '';

			if (!tenant) {
				await goto('/');
				return;
			}

			// パブリックパスの場合はスキップ
			if (isPublicPath(currentPath)) {
				isChecking = false;
				return;
			}

			// チェック中は何もしない
			if (isChecking || cancelled) return;

			// 認証状態を確認
			const isValid = await auth.checkSession(tenant);
			if (cancelled) return;

			if (!isValid && isAuthenticated) {
				// 認証が切れた場合
				isAuthenticated = false;
				const returnUrl = encodeURIComponent(currentPath + $page.url.search + $page.url.hash);
				await goto(`/${tenant}/auth/login?returnUrl=${returnUrl}`);
			} else if (isValid && !isAuthenticated) {
				isAuthenticated = true;
			}
		};

		void run();

		return () => {
			cancelled = true;
		};
	});
</script>

{#if isChecking}
	<div class="flex items-center justify-center min-h-screen">
		<div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
	</div>
{:else}
	{@render children?.()}
{/if}

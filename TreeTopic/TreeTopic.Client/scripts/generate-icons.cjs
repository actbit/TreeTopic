const { createCanvas } = require('canvas');
const fs = require('fs');
const path = require('path');

const sizes = [192, 512];
const staticDir = path.resolve(__dirname, '../static');

// 確保
if (!fs.existsSync(staticDir)) {
  fs.mkdirSync(staticDir, { recursive: true });
}

sizes.forEach(size => {
  const canvas = createCanvas(size, size);
  const ctx = canvas.getContext('2d');

  // 背景
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, size, size);

  // 枠
  ctx.strokeStyle = '#4A5568';
  ctx.lineWidth = size / 24;
  ctx.strokeRect(size / 8, size / 8, (size * 3) / 4, (size * 3) / 4);

  // 木の形（簡易版）
  ctx.fillStyle = '#48BB78';
  ctx.beginPath();
  ctx.arc(size / 2, size / 2.5, size / 4, 0, Math.PI * 2);
  ctx.fill();

  // 幹
  ctx.fillStyle = '#744210';
  ctx.fillRect(size / 2 - size / 24, size / 2.2, size / 12, size / 3);

  // 保存
  const filename = `pwa-${size}x${size}.png`;
  const filepath = path.join(staticDir, filename);
  const buffer = canvas.toBuffer('image/png');
  fs.writeFileSync(filepath, buffer);

  console.log(`Generated ${filename}`);
});

console.log('PWA icons generated successfully!');

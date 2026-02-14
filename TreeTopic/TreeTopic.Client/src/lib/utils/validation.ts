export function isValidEmail(email: string): boolean {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email);
}

export function validatePasswordStrength(password: string): { isValid: boolean; strength: 'weak' | 'medium' | 'strong'; feedback: string[] } {
  const feedback: string[] = [];

  if (password.length < 8) {
    feedback.push('Password must be at least 8 characters');
  }

  if (!/[a-z]/.test(password)) {
    feedback.push('Password must contain lowercase letters');
  }

  if (!/[A-Z]/.test(password)) {
    feedback.push('Password must contain uppercase letters');
  }

  if (!/[0-9]/.test(password)) {
    feedback.push('Password must contain numbers');
  }

  if (!/[!@#$%^&*]/.test(password)) {
    feedback.push('Password must contain special characters (!@#$%^&*)');
  }

  let strength: 'weak' | 'medium' | 'strong' = 'weak';
  if (feedback.length <= 2) strength = 'strong';
  else if (feedback.length <= 3) strength = 'medium';

  return {
    isValid: feedback.length === 0,
    strength,
    feedback,
  };
}

export function isValidUsername(username: string): boolean {
  if (username.length < 3 || username.length > 32) return false;
  return /^[a-zA-Z0-9_-]+$/.test(username);
}

export function isValidUrl(url: string): boolean {
  try {
    new URL(url);
    return true;
  } catch {
    return false;
  }
}

export function isValidFileSize(sizeInBytes: number, maxSizeInMb: number = 50): boolean {
  return sizeInBytes <= maxSizeInMb * 1024 * 1024;
}

export function isValidFileType(file: File, allowedTypes: string[]): boolean {
  return allowedTypes.includes(file.type);
}

export function formatFileSize(sizeInBytes: number): string {
  if (sizeInBytes === 0) return '0 Bytes';

  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(sizeInBytes) / Math.log(k));

  return parseFloat((sizeInBytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

export function isValidHexColor(color: string): boolean {
  return /^#[0-9A-F]{6}$/i.test(color);
}

export function isValidJson(jsonString: string): boolean {
  try {
    JSON.parse(jsonString);
    return true;
  } catch {
    return false;
  }
}

export function isInRange(
  value: number,
  min: number,
  max: number
): boolean {
  return value >= min && value <= max;
}

export function isRequired(value: string | null | undefined): boolean {
  return value !== null && value !== undefined && value.trim().length > 0;
}

export function minLength(value: string, length: number): boolean {
  return value.length >= length;
}

export function maxLength(value: string, length: number): boolean {
  return value.length <= length;
}

export function isValidUUID(uuid: string): boolean {
  const uuidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return uuidRegex.test(uuid);
}

export function isValidGUID(guid: string): boolean {
  const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  return guidRegex.test(guid);
}

export function sanitizeInput(input: string): string {
  const div = document.createElement('div');
  div.textContent = input;
  return div.innerHTML;
}

export function escapeHtml(text: string): string {
  const map: { [key: string]: string } = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;',
  };
  return text.replace(/[&<>"']/g, (char) => map[char]);
}

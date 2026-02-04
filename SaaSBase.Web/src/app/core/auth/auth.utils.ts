const AUTH_KEYS = ['token', 'access_token'];
const META_KEYS = ['refresh_token', 'token_expiry', 'user_data', 'roles'];

const readFromStorages = (key: string): string | null => {
  const ls = localStorage.getItem(key);
  if (ls) return ls;
  const ss = sessionStorage.getItem(key);
  return ss;
};

export const getAccessToken = (): string | null => {
  for (const key of AUTH_KEYS) {
    const val = readFromStorages(key);
    if (val) return val;
  }
  return null;
};

export const getRefreshToken = (): string | null => {
  return readFromStorages('refresh_token');
};

export const getTokenExpiry = (): number | null => {
  const raw = readFromStorages('token_expiry');
  if (!raw) return null;
  const t = Date.parse(raw);
  return isNaN(t) ? null : t;
};

export const isAuthenticated = (): boolean => {
  const token = getAccessToken();
  if (!token) return false;
  const explicitExpiry = getTokenExpiry();
  const jwtExpiry = getJwtExpiryFromToken(token);
  const effectiveExpiry = explicitExpiry ?? jwtExpiry;
  if (effectiveExpiry && Date.now() > effectiveExpiry) return false;
  return true;
};

export const clearAuthStorage = (): void => {
  for (const key of [...AUTH_KEYS, ...META_KEYS]) {
    localStorage.removeItem(key);
    sessionStorage.removeItem(key);
  }
};

export const setAuthData = (data: { accessToken?: string | null; refreshToken?: string | null; expiresAtUtc?: string | null; }): void => {
  const { accessToken, refreshToken, expiresAtUtc } = data;
  if (accessToken) {
    localStorage.setItem('token', accessToken);
    localStorage.setItem('access_token', accessToken);
  }
  if (refreshToken) {
    localStorage.setItem('refresh_token', refreshToken);
  }
  if (expiresAtUtc) {
    localStorage.setItem('token_expiry', expiresAtUtc);
  }
};

export const decodeJwt = (token: string): any | null => {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = atob(payload);
    return JSON.parse(json);
  } catch {
    return null;
  }
};

export const getJwtExpiryFromToken = (token: string): number | null => {
  const payload = decodeJwt(token);
  const exp = payload?.exp;
  if (typeof exp === 'number') {
    return exp * 1000; // exp is seconds since epoch
  }
  return null;
};

export const willExpireWithinMs = (ms: number): boolean => {
  const token = getAccessToken();
  if (!token) return true;
  const explicitExpiry = getTokenExpiry();
  const jwtExpiry = getJwtExpiryFromToken(token);
  const effectiveExpiry = explicitExpiry ?? jwtExpiry;
  if (!effectiveExpiry) return false;
  return Date.now() + ms >= effectiveExpiry;
};



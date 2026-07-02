export const PASSWORD_REQUIREMENT =
  'Password must be at least 8 characters and include uppercase, lowercase, number, and special character.';

const EMAIL_REGEX = /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i;
const PASSWORD_REGEX = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$/;

export function isValidEmail(email) {
  return typeof email === 'string' && email.trim().length <= 255 && EMAIL_REGEX.test(email.trim());
}

export function isValidPassword(password) {
  return typeof password === 'string' && PASSWORD_REGEX.test(password);
}

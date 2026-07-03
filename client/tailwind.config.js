/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{js,jsx,ts,tsx}', './public/index.html'],
  theme: {
    extend: {
      colors: {
        brand: {
          DEFAULT: '#06b6d4',
          50: '#ecfeff',
          100: '#cffafe',
          200: '#a5f3fc',
          300: '#67e8f9',
          400: '#22d3ee',
          500: '#06b6d4',
          600: '#0891b2',
          700: '#0e7490',
          800: '#155e75',
          900: '#164e63',
        },
        accent: {
          DEFAULT: '#f1356d',
          50: '#fef1f5',
          100: '#fde0ea',
          400: '#f4669a',
          500: '#f1356d',
          600: '#d11f56',
        },
        ink: '#1f2533',
        canvas: '#f5f7fb',
      },
      fontFamily: {
        display: ['Quicksand', 'sans-serif'],
        sans: ['Inter', 'sans-serif'],
      },
      boxShadow: {
        soft: '0 18px 45px rgba(24, 28, 40, 0.08)',
        card: '0 8px 24px rgba(31, 37, 51, 0.06)',
        'card-hover': '0 16px 36px rgba(31, 37, 51, 0.12)',
      },
      keyframes: {
        'fade-up': {
          '0%': { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
      },
      animation: {
        'fade-up': 'fade-up 0.4s ease-out both',
      },
    },
  },
  plugins: [],
}


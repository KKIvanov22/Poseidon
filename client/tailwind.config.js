/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{js,jsx,ts,tsx}', './public/index.html'],
  theme: {
    extend: {
      colors: {
        brand: {
          DEFAULT: '#226abd',
          50: '#eef5fc',
          100: '#dbe9f8',
          200: '#aecdef',
          300: '#7eafe4',
          400: '#4a8cd6',
          500: '#226abd',
          600: '#1b529a',
          700: '#163f76',
          800: '#122f58',
          900: '#0d2240',
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


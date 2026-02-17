/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['DM Sans', 'system-ui', 'sans-serif'],
        display: ['Outfit', 'system-ui', 'sans-serif'],
      },
      colors: {
        slate: {
          850: '#172033',
          950: '#0c1222',
        },
        accent: {
          emerald: '#10b981',
          amber: '#f59e0b',
        },
      },
    },
  },
  plugins: [],
}

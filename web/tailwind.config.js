/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        // Public-facing palette — emerald primary (Saudi/federation colours),
        // paired with a deep navy for hero sections.
        smf: {
          50:  "#ecfdf5",
          100: "#d1fae5",
          200: "#a7f3d0",
          300: "#6ee7b7",
          400: "#34d399",
          500: "#10b981",
          600: "#059669",
          700: "#047857",
          800: "#065f46",
          900: "#064e3b",
        },
        navy: {
          700: "#1e293b",
          800: "#0f172a",
          900: "#020617",
        },
      },
      fontFamily: {
        sans: ['"Inter"', "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"],
        heading: ['"Plus Jakarta Sans"', '"Inter"', "system-ui", "sans-serif"],
      },
      boxShadow: {
        "card":     "0 1px 2px 0 rgb(0 0 0 / 0.04), 0 1px 3px 0 rgb(0 0 0 / 0.06)",
        "hero":     "0 30px 60px -20px rgb(6 95 70 / 0.35)",
      },
      backgroundImage: {
        "hero-grid":
          "radial-gradient(circle at 1px 1px, rgb(255 255 255 / 0.12) 1px, transparent 0)",
      },
    },
  },
  plugins: [],
};

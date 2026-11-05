const paths = {
  search: "m21 21-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0",
  wrench: "m14 6 4 4 4-4a7 7 0 0 1-9 9l-7 7-4-4 7-7a7 7 0 0 1 9-9z",
  bell: "M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M9 21h6",
  arrow: "M5 12h14m-6-6 6 6-6 6",
  home: "m3 11 9-8 9 8v10h-7v-7h-4v7H3z",
};
export function Icon({ name }: { name: keyof typeof paths }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      width="24"
      height="24"
      aria-hidden="true"
    >
      <path d={paths[name]} />
    </svg>
  );
}
export function BrandMark() {
  return (
    <svg aria-hidden="true" width="28" height="28" viewBox="0 0 32 32">
      <path d="M3 20 16 5l13 15v7L16 12 3 27z" fill="#e15307" />
      <path d="m5 28 11-10 11 10-11-5z" fill="#144caa" />
    </svg>
  );
}

export function universityEmail(input: string) {
  const email = input.trim();
  const separator = email.lastIndexOf("@");
  if (
    separator < 0 ||
    email.slice(separator + 1).toLowerCase() !== "o6u.edu.eg"
  )
    return false;
  const local = email.slice(0, separator);
  if (!local || local.length > 64) return false;
  if (local.startsWith('"'))
    return /^"(?:[\x20-\x21\x23-\x5b\x5d-\x7e]|\\[\x20-\x7e])*"$/.test(local);
  return /^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*$/i.test(
    local,
  );
}

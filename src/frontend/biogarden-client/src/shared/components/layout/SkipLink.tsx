export default function SkipLink() {
  return <a href="#main-content" className="skip-link" onClick={() => document.getElementById("main-content")?.focus()}>Ugrás a tartalomhoz</a>;
}

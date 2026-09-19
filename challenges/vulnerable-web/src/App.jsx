import { useEffect, useState } from "react";

// CHALLENGE 6 — Stored XSS.
// The board renders each comment's body with dangerouslySetInnerHTML, bypassing React's
// default escaping. A comment containing e.g. <img src=x onerror=...> executes in whoever
// views the board — including the admin bot whose session holds a sensitive cookie.
//
// !!! INTENTIONALLY VULNERABLE — see App.patched.jsx.txt for the fix. !!!
export default function App() {
  const [comments, setComments] = useState([]);
  const [body, setBody] = useState("");

  async function load() {
    const r = await fetch("/api/comments");
    setComments(await r.json());
  }
  useEffect(() => { load(); }, []);

  async function post(e) {
    e.preventDefault();
    if (!body.trim()) return;
    await fetch("/api/comments", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ body }),
    });
    setBody("");
    load();
  }

  return (
    <div className="wrap">
      <header>
        <span className="logo">A</span>
        <h1>Acme Community Board</h1>
      </header>

      <form className="composer" onSubmit={post}>
        <textarea
          value={body}
          onChange={(e) => setBody(e.target.value)}
          placeholder="Leave a comment for the team…"
          rows={3}
        />
        <button type="submit">Post comment</button>
      </form>

      <ul className="comments">
        {comments.map((c) => (
          <li key={c.id}>
            {/* VULNERABLE: renders raw HTML from user input */}
            <div dangerouslySetInnerHTML={{ __html: c.body }} />
          </li>
        ))}
        {comments.length === 0 && <li className="empty">No comments yet.</li>}
      </ul>
    </div>
  );
}

import { useEffect, useState } from "react";

type HealthPayload = {
  status: string;
  service: string;
  timestampUtc: string;
  dependency?: string;
};

type ProbeState = {
  live?: HealthPayload;
  ready?: HealthPayload;
  readyHttpStatus?: number;
  error?: string;
};

async function fetchJson(path: string): Promise<{ status: number; payload: HealthPayload }> {
  const response = await fetch(path, { headers: { Accept: "application/json" } });
  const payload = (await response.json()) as HealthPayload;
  return { status: response.status, payload };
}

export default function App() {
  const [probe, setProbe] = useState<ProbeState>({});

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [live, ready] = await Promise.all([
          fetchJson("/health/live"),
          fetchJson("/health/ready")
        ]);

        if (!cancelled) {
          setProbe({
            live: live.payload,
            ready: ready.payload,
            readyHttpStatus: ready.status
          });
        }
      } catch (error) {
        if (!cancelled) {
          setProbe({
            error: error instanceof Error ? error.message : "Health probe failed."
          });
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  const ready = probe.readyHttpStatus === 200;

  return (
    <main className="shell">
      <section className="summary">
        <div>
          <p className="eyebrow">DevControl Stage 1</p>
          <h1>Developer operations control plane</h1>
          <p className="lede">
            Skeleton deployment proof for the combined API and React service.
          </p>
        </div>
        <div className={ready ? "status status-ready" : "status status-waiting"}>
          <span>{ready ? "Ready" : "Not ready"}</span>
        </div>
      </section>

      <section className="grid">
        <article>
          <h2>Live</h2>
          <p>{probe.live?.status ?? "Checking..."}</p>
        </article>
        <article>
          <h2>PostgreSQL</h2>
          <p>{probe.ready?.status ?? probe.error ?? "Checking..."}</p>
        </article>
        <article>
          <h2>Region</h2>
          <p>us-central1</p>
        </article>
      </section>
    </main>
  );
}


export const dynamic = "force-dynamic";

export function GET() {
  return Response.json({ status: "Healthy" }, { headers: { "Cache-Control": "no-store" } });
}

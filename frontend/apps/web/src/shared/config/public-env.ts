import { readPublicEnv } from "./env.schema";

export const publicEnv = readPublicEnv({ NEXT_PUBLIC_APP_NAME: process.env.NEXT_PUBLIC_APP_NAME });

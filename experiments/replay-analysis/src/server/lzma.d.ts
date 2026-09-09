declare module "lzma" {
  export function decompress(data: number[] | Uint8Array): string | Uint8Array;
  export function compress(data: string | Uint8Array | number[], mode?: number): number[];
}

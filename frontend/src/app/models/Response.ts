export interface BaseResponse {
    correlationId: string;
    ticket: string;
    statusCode: number;
    errors: string[];
    returnCode: string;
    returnDescription: string;
}

export interface ApiResponse {
    baseResp: BaseResponse;
    req: object | null;
    details?: unknown;
}

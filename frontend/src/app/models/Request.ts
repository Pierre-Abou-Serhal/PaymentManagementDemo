export interface BaseRequest {
    correlationId: string;
    userData: { userName: string };
}

export interface ApiRequest {
    baseReq: BaseRequest;
}

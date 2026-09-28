import {waleApiBaseUrl} from "../api/apiClient.ts";

export function getImageUrl(fileId: string, pageNumber: string, serviceName: string) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Images/Image`;
    return `${routeUrl}?fileId=${fileId}&pageNumber=${pageNumber}&serviceName=${serviceName}`;
}

export function getPdfUrl(filename: string | undefined) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Files/Get`;
    return `${routeUrl}?filename=${filename}`;
}
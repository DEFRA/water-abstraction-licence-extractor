import {waleApiBaseUrl} from "../api/apiClient.ts";

export function getThumbnailUrl(fileId: string) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Images/Thumbnail`;
    return `${routeUrl}?fileId=${fileId}&pageNumber=1&serviceName=PdfPig`;
}

export function getImageUrl(filename: string) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Images/Image`;
    return `${routeUrl}?filename=${encodeURIComponent(filename)}`;
}
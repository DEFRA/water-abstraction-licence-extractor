import {waleApiBaseUrl} from "../api/apiClient.ts";

export function getThumbnailUrl(filename: string) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Images/Thumbnail`;
    return `${routeUrl}?filename=${encodeURIComponent(filename)}`;
}

export function  getImageUrl(filename: string) : string {
    const routeUrl = `${waleApiBaseUrl}/BFF/Images/Image`;
    return `${routeUrl}?filename=${encodeURIComponent(filename)}`;
}
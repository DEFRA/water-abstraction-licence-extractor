export function dashesIfNullOrEmpty(str: string | null | undefined): string {
    if (str == null || str === '') {
        return "--";
    }
    return str;
}

export function dashesIfNullOrZero(i: number | null | undefined): string {
    if (i == null || i == 0) {
        return "--";
    }
    return i!.toString();
}
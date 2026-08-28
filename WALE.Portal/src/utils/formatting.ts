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

export function dashesIfNull(i: number | null | undefined): string {
    if (i == null) {
        return "--";
    }
    return i!.toString();
}

export function compareAlphanumeric(a: string | null | undefined, b: string | null | undefined): number {
    return (a ?? '').localeCompare(b ?? '', undefined, {numeric: true, sensitivity: 'base'});
}
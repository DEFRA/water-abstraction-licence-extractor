/// The numbers and their order come from the estate query's CASE, which decides what a licence
/// lands in; these are only the labels for it. Adding one here does not create a queue.
export const LICENCE_QUEUES: { queue: number; label: string }[] = [
    {queue: 1, label: 'Business review open'},
    {queue: 2, label: 'Licence number flagged'},
    {queue: 3, label: 'Aggregate disagreement'},
    {queue: 4, label: 'Issue date flagged'},
    {queue: 5, label: 'Aggregates unverified'},
    {queue: 6, label: 'Linked licences unverified'},
    {queue: 7, label: 'Thin extraction'},
    {queue: 8, label: 'Ready to verify'},
    {queue: 9, label: 'Verified'}
];

class IReadOnlyCollection:
    """ Describes a generic read-only collection of items. """
    # Remarks:
    # To unify identical functionality across different collection-like ADTs, such as the table, list or sorted list, this interface is introduced.
    # ADTs such as these should inherit read-only collection functionality from this, rather than specifying their own version of said functionality.
    # Every read-only collection must implement the '__iter__' method in Python, as specified by the 'iterable<T>' base type.
    # Implementing this allows using the for loop statement on any read-only collection.
    # The exact contracts of 'iterable<T>' and '__iter__' are omitted here, as they are both platform-specific means of enabling a platform-agnostic pattern.
    # Instead, 'iterable<T>' is defined as a base type that supports the use of a for loop on the collection.

    def __iter__(self):
        """ Creates an iterator that iterates over every element in the collection. """
        raise NotImplementedError("Method 'IReadOnlyCollection.__iter__' was not implemented.")

    @property
    def count(self):
        """ Gets the number of elements in the collection. """
        raise NotImplementedError("Getter of property 'IReadOnlyCollection.count' was not implemented.")
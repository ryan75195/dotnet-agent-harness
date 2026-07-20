import { fireEvent, render, screen } from '@testing-library/react-native';

import { LibraryScreen } from '../LibraryScreen';

describe('LibraryScreen', () => {
  test('returns to the previous screen when Back is pressed', () => {
    const onBackPress = jest.fn();
    render(<LibraryScreen onBackPress={onBackPress} />);
    fireEvent.press(screen.getByText('Back'));
    expect(onBackPress).toHaveBeenCalled();
  });
});

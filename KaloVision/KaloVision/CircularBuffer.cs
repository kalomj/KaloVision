using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaloVision
{
    public class CircularBuffer
    {
        Queue<double> _queue;
        int _size;

        public CircularBuffer(int size)
        {
            _queue = new Queue<double>(size);
            _size = size;
        }

        public void Add(double obj)
        {
            if (_queue.Count == _size)
            {
                _queue.Dequeue();
                _queue.Enqueue(obj);
            }
            else
                _queue.Enqueue(obj);
        }
        public double Read()
        {
            return _queue.Dequeue();
        }

        public double Peek()
        {
            return _queue.Peek();
        }

        public double Avg()
        {
            return _queue.Average();
        }

        public int Count()
        {
            return _queue.Count;
        }
    }
}
